#include <algorithm>
#include <array>
#include <atomic>
#include <cstdint>
#include <csignal>
#include <cstring>
#include <iostream>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/audio/format-utils.h>

namespace {

struct App {
  pw_main_loop *main_loop = nullptr;
  pw_stream *stream = nullptr;
  uint64_t frame = 0;
};

std::atomic_bool stop_requested = false;

void on_signal(int) { stop_requested.store(true); }

void on_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(app.stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  const auto requested =
      pw_buffer->requested == 0 ? spa_data.maxsize / sizeof(float)
                                : pw_buffer->requested;
  const auto frames =
      std::min<uint32_t>(requested, spa_data.maxsize / sizeof(float));
  auto *samples = static_cast<float *>(spa_data.data);

  constexpr uint64_t interval_frames = 24'000;
  for (uint32_t frame = 0; frame < frames; ++frame) {
    const auto interval = (app.frame + frame) / interval_frames;
    samples[frame] = (interval % 2) == 0 ? 0.75f : 0.0f;
  }
  app.frame += frames;

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = frames * sizeof(float);
  spa_data.chunk->stride = sizeof(float);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(app.stream, pw_buffer);

  if (stop_requested.load())
    pw_main_loop_quit(app.main_loop);
}

const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_process,
};

} // namespace

int main(int argc, char **argv) {
  pw_init(&argc, &argv);

  App app;
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr)
    return 1;

  auto *loop = pw_main_loop_get_loop(app.main_loop);
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "Test", PW_KEY_MEDIA_CLASS, "Stream/Output/Audio",
      PW_KEY_NODE_NAME, "se.simple_producer", PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy simple producer", "node.passive", "false", nullptr);

  app.stream = pw_stream_new_simple(loop, "se.simple_producer", properties,
                                    &stream_events, &app);
  if (app.stream == nullptr)
    return 1;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto audio_info =
      SPA_AUDIO_INFO_RAW_INIT(.format = SPA_AUDIO_FORMAT_F32, .rate = 48'000,
                              .channels = 1,
                              .position = {SPA_AUDIO_CHANNEL_MONO});
  const spa_pod *params[] = {
      spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &audio_info)};

  const auto result = pw_stream_connect(
      app.stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0) {
    std::cerr << "pw_stream_connect failed: " << result << '\n';
    pw_stream_destroy(app.stream);
    pw_main_loop_destroy(app.main_loop);
    pw_deinit();
    return 1;
  }

  std::signal(SIGINT, on_signal);
  std::signal(SIGTERM, on_signal);
  std::cout << "se.simple_producer running\n";
  pw_main_loop_run(app.main_loop);

  pw_stream_destroy(app.stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
