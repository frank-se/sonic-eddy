#include "Producer.h"

#include <array>
#include <cstring>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/buffers.h>
#include <spa/param/video/format-utils.h>

#include "../logging/log.h"

namespace video {

namespace {
constexpr uint32_t kBytesPerPixel = 4; // SPA_VIDEO_FORMAT_RGBA
} // namespace

const pw_stream_events Producer::kStreamEvents = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = Producer::on_param_changed,
    .process = Producer::on_process,
};

Producer::Producer(pw_loop *loop, ProducerConfig config)
    : _loop(loop), _config(std::move(config)) {}

Producer::~Producer() { stop(); }

bool Producer::start() {
  if (_stream != nullptr)
    return true;

  if (_config.width == 0 || _config.height == 0) {
    logging::log<logging::LogLevel::Error>(
        "video::Producer: invalid width/height ({}x{})", _config.width,
        _config.height);
    return false;
  }

  _frame.assign(
      static_cast<size_t>(_config.width) * _config.height * kBytesPerPixel,
      0);
  _has_frame = false;

  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "Video", PW_KEY_MEDIA_CLASS, "Stream/Output/Video",
      PW_KEY_NODE_NAME, _config.name.c_str(), PW_KEY_NODE_DESCRIPTION,
      _config.description.c_str(), nullptr);

  _stream = pw_stream_new_simple(_loop, _config.name.c_str(), properties,
                                 &kStreamEvents, this);
  if (_stream == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "video::Producer: pw_stream_new_simple failed for '{}'",
        _config.name);
    return false;
  }

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  // framerate.denom == 0 means "unconstrained" in the EnumFormat pod - see
  // pw-video-compositor/src/main.cpp for why a fixed SPA_FRACTION(0, 1)
  // (rather than omitted) breaks negotiation with real consumers.
  auto video_info =
      SPA_VIDEO_INFO_RAW_INIT(.format = SPA_VIDEO_FORMAT_RGBA,
                              .size = SPA_RECTANGLE(_config.width, _config.height),
                              .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {
      spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  const auto result = pw_stream_connect(
      _stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "video::Producer: pw_stream_connect failed for '{}': {}",
        _config.name, result);
    pw_stream_destroy(_stream);
    _stream = nullptr;
    return false;
  }

  return true;
}

void Producer::stop() {
  if (_stream == nullptr)
    return;
  _stopping.store(true, std::memory_order_release);
  pw_stream_destroy(_stream);
  _stream = nullptr;
}

bool Producer::update_frame(const uint8_t *rgba, size_t size) {
  if (rgba == nullptr)
    return false;

  const size_t expected =
      static_cast<size_t>(_config.width) * _config.height * kBytesPerPixel;
  if (size != expected)
    return false;

  std::lock_guard<std::mutex> lock(_frame_mutex);
  std::memcpy(_frame.data(), rgba, size);
  _has_frame = true;
  return true;
}

void Producer::on_process(void *data) {
  auto &self = *static_cast<Producer *>(data);
  if (self._stopping.load(std::memory_order_acquire))
    return;
  auto *pw_buffer = pw_stream_dequeue_buffer(self._stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(self._stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  const uint32_t stride = self._config.width * kBytesPerPixel;
  const size_t needed = static_cast<size_t>(stride) * self._config.height;
  // maxsize is the actual mapped buffer size; a transitional negotiation
  // buffer can report maxsize 0 even though data/chunk are non-null - see
  // pw-video-compositor/src/main.cpp on_output_process for the crash this
  // guards against.
  if (spa_data.maxsize < needed) {
    pw_stream_queue_buffer(self._stream, pw_buffer);
    return;
  }

  {
    std::lock_guard<std::mutex> lock(self._frame_mutex);
    if (self._has_frame)
      std::memcpy(spa_data.data, self._frame.data(), needed);
    else
      std::memset(spa_data.data, 0, needed);
  }

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = static_cast<uint32_t>(needed);
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(self._stream, pw_buffer);
}

// Once the concrete output format is negotiated, declare the buffer size we
// actually need and that buffers carry SPA_META_Header - without either of
// these, real consumers (e.g. GStreamer's pipewiresrc) silently drop every
// buffer. See on_output_param_changed in pw-video-compositor/src/main.cpp
// for the full story of how this was discovered.
void Producer::on_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &self = *static_cast<Producer *>(data);
  if (self._stopping.load(std::memory_order_acquire))
    return;
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  const uint32_t stride = self._config.width * kBytesPerPixel;
  std::array<uint8_t, 512> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  const spa_pod *params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers,
          SPA_PARAM_BUFFERS_buffers, SPA_POD_CHOICE_RANGE_Int(4, 2, 8),
          SPA_PARAM_BUFFERS_blocks, SPA_POD_Int(1), SPA_PARAM_BUFFERS_size,
          SPA_POD_Int(static_cast<int32_t>(stride * self._config.height)),
          SPA_PARAM_BUFFERS_stride, SPA_POD_Int(static_cast<int32_t>(stride)))),
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamMeta, SPA_PARAM_Meta,
          SPA_PARAM_META_type, SPA_POD_Id(SPA_META_Header), SPA_PARAM_META_size,
          SPA_POD_Int(sizeof(spa_meta_header))))};
  pw_stream_update_params(self._stream, params, 2);
}

} // namespace video
