// Small standalone PipeWire node: two RGBA video inputs, one RGBA output,
// linearly cross-dissolves the two inputs based on a "blend_position" float
// (0..1) received over Props. Companion to pw-video-compositor - meant to
// sit downstream of two --instance-name'd compositor instances (see
// node_name() in main.cpp), blending their outputs for a simple T-bar-style
// M/E switcher prototype. Deliberately standalone/self-contained rather than
// sharing main.cpp's App/FrameSource types - the shapes only partially
// overlap and this file is small enough that duplicating them is clearer
// than factoring out a shared header for a two-input-one-output special case.
//
// Scope cuts, all deliberate for this first prototype pass:
// - No scaling: both inputs and the output must already share the same
//   width/height (set via --width/--height, matching both upstream
//   compositors' canvas size) - a mismatched frame is simply blended
//   byte-for-byte against whatever's in the other slot, which will look
//   wrong but won't crash.
// - Inputs are never autoconnected (PW_KEY_TARGET_OBJECT is never set) -
//   video autoconnect is unreliable on this system (confirmed repeatedly
//   elsewhere in this project), so linking source compositors' outputs into
//   se.video-blender.in0/in1 stays a manual `pw-link` step.
#include <algorithm>
#include <array>
#include <atomic>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <mutex>
#include <string>
#include <vector>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/buffers.h>
#include <spa/param/props.h>
#include <spa/param/video/format-utils.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

namespace {

constexpr uint32_t kBytesPerPixel = 4; // SPA_VIDEO_FORMAT_RGBA
constexpr size_t kInputCount = 2;

struct FrameSource {
  std::vector<uint8_t> frame;
  std::mutex frame_mutex;
  bool has_frame = false;
  pw_stream *stream = nullptr;
};

struct App {
  pw_main_loop *main_loop = nullptr;
  uint32_t width = 0;
  uint32_t height = 0;

  std::array<FrameSource, kInputCount> sources;
  pw_stream *out_stream = nullptr;

  // Control thread (param_changed) writes this, process() reads it on the RT
  // graph thread - relaxed is enough since it's an independent scalar
  // "last write wins" value, same idiom as pw-video-compositor's
  // App::active_scene_index (main.cpp).
  std::atomic<float> blend_position{0.0f};
};

void on_input_process(void *data) {
  auto &source = *static_cast<FrameSource *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(source.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(source.stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  // chunk->size alone isn't trustworthy - maxsize is the actual mapped
  // buffer size and can be smaller (e.g. transitional negotiation buffers).
  const size_t copy_size = std::min<size_t>(
      {source.frame.size(), spa_data.chunk->size, static_cast<size_t>(spa_data.maxsize)});
  if (copy_size > 0) {
    std::lock_guard<std::mutex> lock(source.frame_mutex);
    std::memcpy(source.frame.data(), spa_data.data, copy_size);
    source.has_frame = true;
  }

  pw_stream_queue_buffer(source.stream, pw_buffer);
}

void on_output_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.out_stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(app.out_stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  const uint32_t stride = app.width * kBytesPerPixel;
  const size_t needed = static_cast<size_t>(stride) * app.height;
  // maxsize is the actual mapped buffer size; a transitional negotiation
  // buffer can report maxsize 0 even though data/chunk are non-null.
  if (spa_data.maxsize < needed) {
    pw_stream_queue_buffer(app.out_stream, pw_buffer);
    return;
  }
  auto *dst = static_cast<uint8_t *>(spa_data.data);

  const float t = app.blend_position.load(std::memory_order_relaxed);

  std::lock_guard<std::mutex> lock_a(app.sources[0].frame_mutex);
  std::lock_guard<std::mutex> lock_b(app.sources[1].frame_mutex);
  const bool has_a = app.sources[0].has_frame;
  const bool has_b = app.sources[1].has_frame;
  const uint8_t *a = has_a ? app.sources[0].frame.data() : nullptr;
  const uint8_t *b = has_b ? app.sources[1].frame.data() : nullptr;

  // A missing source is treated as all-zero (black/transparent), matching
  // pw-video-compositor's own graceful-degradation behavior for objects
  // whose source hasn't produced a frame yet. Blended uniformly across all
  // 4 channels (including alpha) - no special-casing needed for a simple
  // linear cross-dissolve.
  for (size_t i = 0; i < needed; ++i) {
    const float av = a != nullptr ? static_cast<float>(a[i]) : 0.0f;
    const float bv = b != nullptr ? static_cast<float>(b[i]) : 0.0f;
    dst[i] = static_cast<uint8_t>(
        std::clamp(av * (1.0f - t) + bv * t, 0.0f, 255.0f));
  }

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = static_cast<uint32_t>(needed);
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(app.out_stream, pw_buffer);
}

void on_quit_signal(void *data, int) {
  auto &app = *static_cast<App *>(data);
  pw_main_loop_quit(app.main_loop);
}

// Mirrors pw-video-compositor's handle_output_props() (main.cpp): walk the
// SPA_PROP_params struct treating even entries as keys and odd as values.
// Only one key is recognized here, "blend_position" (float, clamped 0..1).
void handle_output_props(App &app, const spa_pod *param) {
  const auto *params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr || params_prop->value.type != SPA_TYPE_Struct)
    return;

  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(static_cast<spa_pod *>(SPA_POD_BODY(&params_prop->value)),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key != nullptr && std::strcmp(key, "blend_position") == 0) {
      float value = 0.0f;
      if (spa_pod_get_float(child, &value) == 0)
        app.blend_position.store(std::clamp(value, 0.0f, 1.0f),
                                 std::memory_order_relaxed);
    }
    ++index;
  }
}

// Once the concrete output format is negotiated, declare the buffer size we
// actually need and that buffers carry SPA_META_Header - same rationale as
// pw-video-compositor's on_output_param_changed (main.cpp): without
// ParamBuffers/ParamMeta(Header), consumers like GStreamer's pipewiresrc
// silently starve/drop every buffer.
void on_output_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &app = *static_cast<App *>(data);
  if (param == nullptr)
    return;

  if (id == SPA_PARAM_Props) {
    handle_output_props(app, param);
    return;
  }
  if (id != SPA_PARAM_Format)
    return;

  const uint32_t stride = app.width * kBytesPerPixel;
  std::array<uint8_t, 512> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  const spa_pod *params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers,
          SPA_PARAM_BUFFERS_buffers, SPA_POD_CHOICE_RANGE_Int(4, 2, 8),
          SPA_PARAM_BUFFERS_blocks, SPA_POD_Int(1), SPA_PARAM_BUFFERS_size,
          SPA_POD_Int(stride * app.height), SPA_PARAM_BUFFERS_stride,
          SPA_POD_Int(stride))),
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamMeta, SPA_PARAM_Meta,
          SPA_PARAM_META_type, SPA_POD_Id(SPA_META_Header), SPA_PARAM_META_size,
          SPA_POD_Int(sizeof(spa_meta_header))))};
  pw_stream_update_params(app.out_stream, params, 2);
}

const pw_stream_events input_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_input_process,
};

const pw_stream_events output_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_output_param_changed,
    .process = on_output_process,
};

pw_stream *connect_video_stream(pw_loop *loop, const char *name,
                                 const char *media_class,
                                 pw_direction direction,
                                 const pw_stream_events *events, void *user_data,
                                 uint32_t width, uint32_t height) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY,
      direction == PW_DIRECTION_INPUT ? "Capture" : "Playback",
      PW_KEY_MEDIA_ROLE, "Video", PW_KEY_MEDIA_CLASS, media_class,
      PW_KEY_NODE_NAME, name, PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy video blender", nullptr);
  // Deliberately no PW_KEY_TARGET_OBJECT - video autoconnect is unreliable
  // here, linking stays a manual `pw-link` step (see file header).

  auto *stream = pw_stream_new_simple(loop, name, properties, events, user_data);
  if (stream == nullptr)
    return nullptr;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = SPA_VIDEO_FORMAT_RGBA,
                                            .size = SPA_RECTANGLE(width, height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {
      spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  const auto result = pw_stream_connect(
      stream, direction, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0) {
    std::cerr << name << ": pw_stream_connect failed: " << result << '\n';
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

} // namespace

int main(int argc, char **argv) {
  App app;

  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    auto next = [&]() { return std::string(i + 1 < argc ? argv[++i] : ""); };
    if (arg == "--width")
      app.width = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--height")
      app.height = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else {
      std::cerr << "unknown argument: " << arg << '\n';
      return 1;
    }
  }
  if (app.width == 0 || app.height == 0) {
    std::cerr << "usage: video-blender --width W --height H\n";
    return 1;
  }

  for (auto &source : app.sources)
    source.frame.assign(static_cast<size_t>(app.width) * app.height * kBytesPerPixel, 0);

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr) {
    std::cerr << "pw_main_loop_new failed\n";
    return 1;
  }
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  static const char *kInputNames[kInputCount] = {"se.video-blender.in0",
                                                 "se.video-blender.in1"};
  for (size_t idx = 0; idx < kInputCount; ++idx) {
    app.sources[idx].stream = connect_video_stream(
        loop, kInputNames[idx], "Stream/Input/Video", PW_DIRECTION_INPUT,
        &input_stream_events, &app.sources[idx], app.width, app.height);
    if (app.sources[idx].stream == nullptr)
      return 1;
  }

  app.out_stream = connect_video_stream(loop, "se.video-blender.out",
                                        "Stream/Output/Video",
                                        PW_DIRECTION_OUTPUT, &output_stream_events,
                                        &app, app.width, app.height);
  if (app.out_stream == nullptr)
    return 1;

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  std::cout << "video-blender running (" << app.width << "x" << app.height
            << ")\n" << std::flush;
  pw_main_loop_run(app.main_loop);

  for (auto &source : app.sources)
    if (source.stream != nullptr)
      pw_stream_destroy(source.stream);
  pw_stream_destroy(app.out_stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
