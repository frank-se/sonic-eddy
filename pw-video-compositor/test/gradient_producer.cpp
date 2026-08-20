// Throwaway pure-PipeWire test tool (no GStreamer): pushes a deterministic
// RGB gradient into a named PipeWire node, so the compositor's negotiation
// and compositing math can be verified without an external media framework.
#include <array>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <string>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/video/format-utils.h>

namespace {

constexpr uint32_t kBytesPerPixel = 3;

struct App {
  pw_main_loop *main_loop = nullptr;
  pw_stream *stream = nullptr;
  uint32_t width = 0;
  uint32_t height = 0;
  uint8_t blue = 0; // fixed B channel value identifying this producer
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
  const uint32_t stride = app.width * kBytesPerPixel;
  auto *dst = static_cast<uint8_t *>(spa_data.data);
  for (uint32_t y = 0; y < app.height; ++y) {
    uint8_t *row = dst + static_cast<size_t>(y) * stride;
    const uint8_t g = static_cast<uint8_t>((y * 255) / (app.height - 1));
    for (uint32_t x = 0; x < app.width; ++x) {
      const uint8_t r = static_cast<uint8_t>((x * 255) / (app.width - 1));
      row[x * kBytesPerPixel + 0] = r;
      row[x * kBytesPerPixel + 1] = g;
      row[x * kBytesPerPixel + 2] = app.blue;
    }
  }

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = stride * app.height;
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(app.stream, pw_buffer);
}

void on_quit_signal(void *data, int) {
  auto &app = *static_cast<App *>(data);
  pw_main_loop_quit(app.main_loop);
}

const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
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
    else {
      std::cerr << "unknown argument: " << arg << '\n';
      return 1;
    }
  }
  if (target_object.empty() || app.width == 0 || app.height == 0) {
    std::cerr << "usage: gradient_producer --target <node-name> --width W "
                 "--height H --blue N\n";
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
      target_object.c_str(), nullptr);

  app.stream = pw_stream_new_simple(loop, "se.gradient_producer", properties,
                                    &stream_events, &app);
  if (app.stream == nullptr)
    return 1;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = SPA_VIDEO_FORMAT_RGB,
                                            .size = SPA_RECTANGLE(app.width, app.height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {
      spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  const auto result = pw_stream_connect(
      app.stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
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
