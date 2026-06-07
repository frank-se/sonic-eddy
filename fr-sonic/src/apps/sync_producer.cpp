#include "sync/SyncClient.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <csignal>
#include <memory>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/audio/format-utils.h>

namespace {

struct App {
  pw_main_loop *main_loop = nullptr;
  pw_context *context = nullptr;
  pw_core *core = nullptr;
  pw_stream *stream = nullptr;
  std::shared_ptr<sesync::SyncClient> sync_client;
  uint32_t last_frames = 1024;
};

std::atomic_bool stop_requested = false;

void on_signal(int) { stop_requested.store(true); }

uint64_t frame_nsec(const uint32_t frames, const uint32_t sample_rate) {
  return (static_cast<uint64_t>(frames) * 1'000'000'000ull) / sample_rate;
}

void render(float *samples, const uint32_t frames, const uint32_t sample_rate,
            const uint64_t cycle_start_nsec,
            const sesync::SyncSnapshot &snapshot) {
  const auto cycle_end_nsec =
      cycle_start_nsec + frame_nsec(frames, sample_rate);
  const auto &beats = snapshot.beat_history;
  if (beats.size() < 2)
    return;

  for (auto current = beats.begin(); current != beats.end(); ++current) {
    const auto next = std::next(current);
    if (next == beats.end() || next->nsec <= current->nsec)
      break;
    if (next->nsec <= cycle_start_nsec)
      continue;
    if (current->nsec >= cycle_end_nsec)
      break;
    if (snapshot.transport_state_at(current->beat) ==
        sesync::TransportState::Stopped)
      continue;

    const auto interval = next->nsec - current->nsec;
    for (uint32_t pulse = 0; pulse < 24; ++pulse) {
      const auto pulse_nsec = current->nsec + (interval * pulse) / 24;
      const auto pulse_end_nsec = pulse_nsec + 5'000'000;
      if (pulse_end_nsec <= cycle_start_nsec || pulse_nsec >= cycle_end_nsec)
        continue;

      const auto to_frame = [sample_rate](const uint64_t nsec) {
        return (nsec * sample_rate + 999'999'999ull) / 1'000'000'000ull;
      };
      const auto start = pulse_nsec <= cycle_start_nsec
                             ? uint64_t{0}
                             : to_frame(pulse_nsec - cycle_start_nsec);
      const auto end = pulse_end_nsec <= cycle_start_nsec
                           ? uint64_t{0}
                           : to_frame(pulse_end_nsec - cycle_start_nsec);
      std::fill(samples + std::min<uint64_t>(start, frames),
                samples + std::min<uint64_t>(end, frames), 0.75f);
    }
  }
}

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
  auto frames =
      pw_buffer->requested == 0 ? app.last_frames : pw_buffer->requested;
  frames = std::min<uint32_t>(frames, spa_data.maxsize / sizeof(float));
  app.last_frames = frames;
  auto *samples = static_cast<float *>(spa_data.data);
  std::fill_n(samples, frames, 0.0f);

  pw_time stream_time{};
  uint32_t sample_rate = 48'000;
  uint64_t cycle_start_nsec = 0;
  if (pw_stream_get_time_n(app.stream, &stream_time, sizeof(stream_time)) >= 0) {
    if (stream_time.rate.denom > 0)
      sample_rate = stream_time.rate.denom;
    if (stream_time.now > 0)
      cycle_start_nsec = static_cast<uint64_t>(stream_time.now);
  }

  const auto snapshot = app.sync_client->snapshot();
  if (snapshot && cycle_start_nsec > 0)
    render(samples, frames, sample_rate, cycle_start_nsec, *snapshot);

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
  auto *loop = pw_main_loop_get_loop(app.main_loop);
  app.context = pw_context_new(loop, nullptr, 0);
  app.core = pw_context_connect(app.context, nullptr, 0);
  if (app.main_loop == nullptr || app.context == nullptr || app.core == nullptr)
    return 1;

  app.sync_client = std::make_shared<sesync::SyncClient>(app.core, loop);
  app.sync_client->start();

  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "Test", PW_KEY_MEDIA_CLASS, "Stream/Output/Audio",
      PW_KEY_NODE_NAME, "se.sync_producer", PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy isolated sync producer", "node.passive", "false", nullptr);
  app.stream =
      pw_stream_new(app.core, "se.sync_producer", properties);
  pw_stream_add_listener(app.stream, new spa_hook{}, &stream_events, &app);

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto audio_info =
      SPA_AUDIO_INFO_RAW_INIT(.format = SPA_AUDIO_FORMAT_F32, .channels = 1,
                              .position = {SPA_AUDIO_CHANNEL_MONO});
  const spa_pod *params[] = {
      spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &audio_info)};
  if (pw_stream_connect(
          app.stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
          static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                       PW_STREAM_FLAG_RT_PROCESS),
          params, 1) < 0)
    return 1;

  std::signal(SIGINT, on_signal);
  std::signal(SIGTERM, on_signal);
  pw_main_loop_run(app.main_loop);

  app.sync_client->stop();
  pw_stream_destroy(app.stream);
  pw_core_disconnect(app.core);
  pw_context_destroy(app.context);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
