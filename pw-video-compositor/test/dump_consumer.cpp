// Throwaway pure-PipeWire test tool: connects to a named node and dumps the
// Nth received RGB frame to a PPM file, so compositor output can be checked
// programmatically without an external media framework.
#include <array>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iostream>
#include <string>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/video/format-utils.h>

namespace {

struct App {
  pw_main_loop *main_loop = nullptr;
  pw_stream *stream = nullptr;
  uint32_t width = 0;
  uint32_t height = 0;
  uint32_t bytes_per_pixel = 3; // 3 = RGB (gradient_producer/compositor CLI mode), 4 = RGBA (compositor scene mode, midi-cube)
  spa_video_format format = SPA_VIDEO_FORMAT_RGB;
  uint32_t skip_frames = 5; // let the graph settle before capturing
  std::string out_path;
};

// Always writes a PPM (RGB, no alpha) regardless of the source format -
// drops the alpha byte for RGBA sources, since PPM has no alpha channel.
void write_ppm(const App &app, const uint8_t *src, uint32_t stride) {
  std::ofstream out(app.out_path, std::ios::binary);
  out << "P6\n" << app.width << ' ' << app.height << "\n255\n";
  for (uint32_t y = 0; y < app.height; ++y) {
    const uint8_t *row = src + static_cast<size_t>(y) * stride;
    for (uint32_t x = 0; x < app.width; ++x)
      out.write(reinterpret_cast<const char *>(row + static_cast<size_t>(x) * app.bytes_per_pixel), 3);
  }
}

void on_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas > 0 && buffer->datas[0].data != nullptr) {
    if (app.skip_frames > 0) {
      --app.skip_frames;
    } else {
      auto &spa_data = buffer->datas[0];
      const uint32_t stride = app.width * app.bytes_per_pixel;
      write_ppm(app, static_cast<const uint8_t *>(spa_data.data), stride);
      std::cout << "wrote " << app.out_path << '\n';
      pw_stream_queue_buffer(app.stream, pw_buffer);
      pw_main_loop_quit(app.main_loop);
      return;
    }
  }

  pw_stream_queue_buffer(app.stream, pw_buffer);
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
    else if (arg == "--out")
      app.out_path = next();
    else if (arg == "--format") {
      const std::string format = next();
      if (format == "rgba") {
        app.bytes_per_pixel = 4;
        app.format = SPA_VIDEO_FORMAT_RGBA;
      } else if (format != "rgb") {
        std::cerr << "--format must be \"rgb\" or \"rgba\"\n";
        return 1;
      }
    } else {
      std::cerr << "unknown argument: " << arg << '\n';
      return 1;
    }
  }
  if (target_object.empty() || app.width == 0 || app.height == 0 || app.out_path.empty()) {
    std::cerr << "usage: dump_consumer --target <node-name> --width W "
                 "--height H --out <file.ppm> [--format rgb|rgba]\n";
    return 1;
  }

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr)
    return 1;
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "Video", PW_KEY_MEDIA_CLASS, "Stream/Input/Video",
      PW_KEY_TARGET_OBJECT, target_object.c_str(), nullptr);

  app.stream = pw_stream_new_simple(loop, "se.dump_consumer", properties,
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

  const auto result = pw_stream_connect(
      app.stream, PW_DIRECTION_INPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0) {
    std::cerr << "pw_stream_connect failed: " << result << '\n';
    return 1;
  }

  pw_loop_add_signal(
      loop, SIGINT, [](void *data, int) { pw_main_loop_quit(static_cast<App *>(data)->main_loop); }, &app);
  pw_loop_add_signal(
      loop, SIGTERM, [](void *data, int) { pw_main_loop_quit(static_cast<App *>(data)->main_loop); }, &app);
  std::cout << "dump_consumer running, target=" << target_object << '\n' << std::flush;
  pw_main_loop_run(app.main_loop);

  pw_stream_destroy(app.stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
