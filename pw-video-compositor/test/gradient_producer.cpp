// Throwaway pure-PipeWire test tool (no GStreamer): pushes a deterministic
// gradient (RGB by default, --format rgba for the format real camera
// scripts actually use) into a named PipeWire node, so the compositor's
// negotiation and compositing math can be verified without an external
// media framework.
#include <array>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <string>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/buffers.h>
#include <spa/param/video/format-utils.h>
#include <spa/pod/builder.h>

namespace {

struct App {
  pw_main_loop *main_loop = nullptr;
  pw_stream *stream = nullptr;
  uint32_t width = 0;
  uint32_t height = 0;
  uint8_t blue = 0; // fixed B channel value identifying this producer
  // RGB (3 bytes/pixel) by default - this tool's original, still-default
  // format. --format rgba switches to RGBA (4 bytes/pixel), matching the
  // convention every real camera script in this repo actually uses -
  // needed to test ingestion code that (correctly) only accepts RGBA, not
  // RGB (confirmed 2026-09-09: RGB-only was silently failing to negotiate
  // at all against an RGBA-only consumer, no error either side).
  uint32_t bytes_per_pixel = 3;
  spa_video_format format = SPA_VIDEO_FORMAT_RGB;
};

void on_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr) {
    pw_stream_queue_buffer(app.stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  const uint32_t stride = app.width * app.bytes_per_pixel;
  // Never trust the negotiated size blindly - same rule as every other RT
  // process() callback in this codebase (main.cpp's on_input_process,
  // av_sync_record.cpp's clamps). This was missing here and caused a real
  // segfault (2026-09-09) the first time this stream actually connected and
  // negotiated with a peer, rather than idling with target-not-found.
  if (static_cast<size_t>(stride) * app.height > spa_data.maxsize) {
    pw_stream_queue_buffer(app.stream, pw_buffer);
    return;
  }
  auto *dst = static_cast<uint8_t *>(spa_data.data);
  for (uint32_t y = 0; y < app.height; ++y) {
    uint8_t *row = dst + static_cast<size_t>(y) * stride;
    const uint8_t g = static_cast<uint8_t>((y * 255) / (app.height - 1));
    for (uint32_t x = 0; x < app.width; ++x) {
      const uint8_t r = static_cast<uint8_t>((x * 255) / (app.width - 1));
      row[x * app.bytes_per_pixel + 0] = r;
      row[x * app.bytes_per_pixel + 1] = g;
      row[x * app.bytes_per_pixel + 2] = app.blue;
      if (app.bytes_per_pixel == 4)
        row[x * app.bytes_per_pixel + 3] = 255; // RGBA - fully opaque, matching real camera output
    }
  }

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = stride * app.height;
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(app.stream, pw_buffer);
}

// Once the concrete output format is negotiated, declare the buffer size we
// actually need - same rationale as pw-video-compositor's own
// on_output_param_changed (main.cpp) and video_blender.cpp's: without this,
// PipeWire allocates buffers at some small default size (observed: 8192
// bytes, nowhere near a real frame), and on_process's own bounds check then
// correctly refuses to write into them - every frame silently dropped, or
// (before that bounds check existed) a real buffer overflow/segfault. This
// was missing here from the start; never caught because this tool never
// successfully autoconnected to a peer before (see git history) until the
// AUTOCONNECT fix, so the mismatch was never actually exercised.
void on_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &app = *static_cast<App *>(data);
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  const uint32_t stride = app.width * app.bytes_per_pixel;
  std::array<uint8_t, 512> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  const spa_pod *params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers,
          SPA_PARAM_BUFFERS_buffers, SPA_POD_CHOICE_RANGE_Int(4, 2, 8),
          SPA_PARAM_BUFFERS_blocks, SPA_POD_Int(1), SPA_PARAM_BUFFERS_size,
          SPA_POD_Int(stride * app.height), SPA_PARAM_BUFFERS_stride,
          SPA_POD_Int(stride)))};
  pw_stream_update_params(app.stream, params, 1);
}

void on_quit_signal(void *data, int) {
  auto &app = *static_cast<App *>(data);
  pw_main_loop_quit(app.main_loop);
}

const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_param_changed,
    .process = on_process,
};

} // namespace

int main(int argc, char **argv) {
  App app;
  std::string target_object;

  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    auto next = [&]() { return std::string(i + 1 < argc ? argv[++i] : ""); };
    if (arg == "--target")
      target_object = next();
    else if (arg == "--width")
      app.width = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--height")
      app.height = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--blue")
      app.blue = static_cast<uint8_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--format") {
      const std::string format = next();
      if (format == "rgba") {
        app.format = SPA_VIDEO_FORMAT_RGBA;
        app.bytes_per_pixel = 4;
      } else if (format == "rgb") {
        app.format = SPA_VIDEO_FORMAT_RGB;
        app.bytes_per_pixel = 3;
      } else {
        std::cerr << "--format must be rgb or rgba\n";
        return 1;
      }
    } else {
      std::cerr << "unknown argument: " << arg << '\n';
      return 1;
    }
  }
  if (target_object.empty() || app.width == 0 || app.height == 0) {
    std::cerr << "usage: gradient_producer --target <node-name> --width W "
                 "--height H --blue N [--format rgb|rgba]\n";
    return 1;
  }

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr)
    return 1;
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "Video", PW_KEY_MEDIA_CLASS, "Stream/Output/Video",
      PW_KEY_NODE_NAME, "se.gradient_producer", PW_KEY_TARGET_OBJECT,
      target_object.c_str(),
      // Every PW_KEY_TARGET_OBJECT stream in this codebase must pair it with
      // these two - see video_blender.cpp's connect_video_stream comment.
      "node.dont-fallback", "true", "node.linger", "true", nullptr);

  app.stream = pw_stream_new_simple(loop, "se.gradient_producer", properties,
                                    &stream_events, &app);
  if (app.stream == nullptr)
    return 1;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = app.format,
                                            .size = SPA_RECTANGLE(app.width, app.height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {
      spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  // AUTOCONNECT is required alongside PW_KEY_TARGET_OBJECT for WirePlumber
  // to actually attempt a link - target_object alone does nothing (see
  // main.cpp's connect_video_stream comment for the same rule, confirmed
  // empirically 2026-09-09 when this was missing here: both nodes existed
  // in the graph but were never linked).
  const auto result = pw_stream_connect(
      app.stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS |
                                   PW_STREAM_FLAG_AUTOCONNECT),
      params, 1);
  if (result < 0) {
    std::cerr << "pw_stream_connect failed: " << result << '\n';
    return 1;
  }

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  std::cout << "gradient_producer running, target=" << target_object << '\n' << std::flush;
  pw_main_loop_run(app.main_loop);

  pw_stream_destroy(app.stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
