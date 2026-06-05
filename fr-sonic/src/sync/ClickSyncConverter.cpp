#include "ClickSyncConverter.h"

#include "logging/log.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <format>
#include <limits>
#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <spa/param/audio/format-utils.h>

namespace {

const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = sesync::ClickSyncConverter::process_callback,
};

uint64_t frame_nsec(const uint32_t frames, const uint32_t sample_rate) {
  return (static_cast<uint64_t>(frames) * 1'000'000'000ull) / sample_rate;
}

} // namespace

sesync::ClickSyncConverter::ClickSyncConverter(
    pw_loop *loop, ClickSyncConfig config,
    std::shared_ptr<SyncClient> sync_client)
    : _loop(loop), _config(std::move(config)),
      _sync_client(std::move(sync_client)) {}

sesync::ClickSyncConverter::~ClickSyncConverter() { stop(); }

bool sesync::ClickSyncConverter::start() {
  if (_click_output.stream != nullptr || _reset_output.stream != nullptr ||
      _run_output.stream != nullptr)
    return true;

  if (!setup_output(_click_output)) {
    stop();
    return false;
  }
  if (!setup_output(_reset_output)) {
    stop();
    return false;
  }
  if (!setup_output(_run_output)) {
    stop();
    return false;
  }
  return true;
}

void sesync::ClickSyncConverter::stop() {
  if (_click_output.stream != nullptr) {
    pw_stream_destroy(_click_output.stream);
    _click_output.stream = nullptr;
  }
  if (_reset_output.stream != nullptr) {
    pw_stream_destroy(_reset_output.stream);
    _reset_output.stream = nullptr;
  }
  if (_run_output.stream != nullptr) {
    pw_stream_destroy(_run_output.stream);
    _run_output.stream = nullptr;
  }
}

bool sesync::ClickSyncConverter::setup_output(Output &output) {
  const auto name = node_name(output.kind);
  const auto node_description = description(output.kind);
  const auto purpose = output.kind == OutputKind::Click
                           ? "click-sync-click"
                           : output.kind == OutputKind::Reset
                                 ? "click-sync-reset"
                                 : "click-sync-run";
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Output/Audio",
      PW_KEY_NODE_NAME, name.c_str(), PW_KEY_NODE_DESCRIPTION,
      node_description.c_str(), "se.role", purpose, "pmx.purpose", purpose,
      "node.linger", "true", "node.passive", "false",
      PW_KEY_NODE_ALWAYS_PROCESS, "true", nullptr);
  if (!_config.tag.empty())
    pw_properties_set(properties, "pmx.tag", _config.tag.c_str());

  output.stream =
      pw_stream_new_simple(_loop, name.c_str(), properties, &stream_events,
                           static_cast<void *>(&output));
  if (output.stream == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create click sync stream '{}'", name);
    return false;
  }

  std::array<uint8_t, 1024> buffer{};
  auto builder = SPA_POD_BUILDER_INIT(buffer.data(), buffer.size());
  auto audio_info =
      SPA_AUDIO_INFO_RAW_INIT(.format = SPA_AUDIO_FORMAT_F32, .channels = 1,
                              .position = {SPA_AUDIO_CHANNEL_MONO});
  const spa_pod *params[1] = {
      spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &audio_info)};

  const auto result = pw_stream_connect(
      output.stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "Failed to connect click sync stream '{}': {}", name, result);
    return false;
  }
  return true;
}

void sesync::ClickSyncConverter::process(Output &output) {
  if (output.stream == nullptr)
    return;

  auto *pipewire_buffer = pw_stream_dequeue_buffer(output.stream);
  if (pipewire_buffer == nullptr)
    return;

  auto *buffer = pipewire_buffer->buffer;
  if (buffer->n_datas != 1 || buffer->datas[0].data == nullptr) {
    pw_stream_queue_buffer(output.stream, pipewire_buffer);
    return;
  }

  auto *data = &buffer->datas[0];
  auto frames = pipewire_buffer->requested > 0 ? pipewire_buffer->requested
                                               : output.last_cycle_frames;
  frames = std::min(frames, data->maxsize / sizeof(float));
  output.last_cycle_frames = frames;

  auto *samples = static_cast<float *>(data->data);
  std::fill_n(samples, frames, 0.0f);

  pw_time stream_time{};
  uint32_t sample_rate = 48'000;
  uint64_t cycle_start_nsec = 0;
  if (pw_stream_get_time_n(output.stream, &stream_time, sizeof(stream_time)) >=
      0) {
    if (stream_time.rate.denom > 0)
      sample_rate = stream_time.rate.denom;
    if (stream_time.now > 0)
      cycle_start_nsec = static_cast<uint64_t>(stream_time.now);
  }

  const auto snapshot = _sync_client ? _sync_client->snapshot() : nullptr;
  if (snapshot && cycle_start_nsec > 0) {
    if (output.kind == OutputKind::Click)
      render_click(samples, frames, sample_rate, cycle_start_nsec, *snapshot);
    else if (output.kind == OutputKind::Reset)
      render_reset(samples, frames, sample_rate, cycle_start_nsec, *snapshot);
    else
      render_run(samples, frames, sample_rate, cycle_start_nsec, *snapshot);
  }

  data->chunk->offset = 0;
  data->chunk->size = frames * sizeof(float);
  data->chunk->stride = sizeof(float);
  data->chunk->flags = 0;
  pw_stream_queue_buffer(output.stream, pipewire_buffer);
}

void sesync::ClickSyncConverter::render_click(
    float *samples, const uint32_t frames, const uint32_t sample_rate,
    const uint64_t cycle_start_nsec, const SyncSnapshot &snapshot) const {
  if (_config.pulses_per_quarter_note == 0)
    return;

  const auto cycle_end_nsec =
      cycle_start_nsec + frame_nsec(frames, sample_rate);
  const auto &beats = snapshot.beat_history;
  for (auto current = beats.begin(); current != beats.end(); ++current) {
    const auto next = std::next(current);
    if (next == beats.end() || next->nsec <= current->nsec)
      break;
    if (snapshot.transport_state_at(current->beat) == TransportState::Stopped)
      continue;

    const auto beat_interval = next->nsec - current->nsec;
    for (uint32_t pulse = 0; pulse < _config.pulses_per_quarter_note; ++pulse) {
      const auto pulse_nsec =
          current->nsec +
          (beat_interval * pulse) / _config.pulses_per_quarter_note;
      const auto next_pulse_nsec =
          current->nsec +
          (beat_interval * (pulse + 1)) / _config.pulses_per_quarter_note;
      const auto max_duration = (next_pulse_nsec - pulse_nsec) / 2;
      if (pulse_nsec >= cycle_end_nsec)
        break;
      render_pulse(samples, frames, sample_rate, cycle_start_nsec, pulse_nsec,
                   max_duration);
    }
  }
}

void sesync::ClickSyncConverter::render_reset(
    float *samples, const uint32_t frames, const uint32_t sample_rate,
    const uint64_t cycle_start_nsec, const SyncSnapshot &snapshot) const {
  const auto cycle_end_nsec =
      cycle_start_nsec + frame_nsec(frames, sample_rate);
  auto previous_state = TransportState::Stopped;

  for (auto entry = snapshot.transport_states.begin();
       entry != snapshot.transport_states.end();) {
    const auto beat = entry->beat;
    auto state = entry->state;
    while (++entry != snapshot.transport_states.end() && entry->beat == beat)
      state = entry->state;

    if (previous_state == TransportState::Stopped &&
        state != TransportState::Stopped) {
      const auto beat_entry = std::ranges::find(snapshot.beat_history, beat,
                                                &BeatScheduleEntry::beat);
      if (beat_entry != snapshot.beat_history.end() &&
          beat_entry->nsec < cycle_end_nsec) {
        uint64_t max_duration = std::numeric_limits<uint64_t>::max();
        auto later = entry;
        auto later_state = state;
        while (later != snapshot.transport_states.end()) {
          const auto later_beat = later->beat;
          auto grouped_state = later->state;
          while (++later != snapshot.transport_states.end() &&
                 later->beat == later_beat)
            grouped_state = later->state;
          if (later_state == TransportState::Stopped &&
              grouped_state != TransportState::Stopped) {
            const auto next_start = std::ranges::find(
                snapshot.beat_history, later_beat, &BeatScheduleEntry::beat);
            if (next_start != snapshot.beat_history.end() &&
                next_start->nsec > beat_entry->nsec)
              max_duration = (next_start->nsec - beat_entry->nsec) / 2;
            break;
          }
          later_state = grouped_state;
        }
        render_pulse(samples, frames, sample_rate, cycle_start_nsec,
                     beat_entry->nsec, max_duration);
      }
    }
    previous_state = state;
  }
}

void sesync::ClickSyncConverter::render_run(
    float *samples, const uint32_t frames, const uint32_t sample_rate,
    const uint64_t cycle_start_nsec, const SyncSnapshot &snapshot) const {
  if (snapshot.beat_history.empty())
    return;

  for (uint32_t frame = 0; frame < frames; ++frame) {
    const auto sample_nsec =
        cycle_start_nsec + frame_nsec(frame, sample_rate);
    const auto next = std::ranges::upper_bound(
        snapshot.beat_history, sample_nsec, {}, &BeatScheduleEntry::nsec);
    if (next == snapshot.beat_history.begin())
      continue;

    const auto &beat = *std::prev(next);
    if (snapshot.transport_state_at(beat.beat) != TransportState::Stopped)
      samples[frame] = _config.pulse_amplitude;
  }
}

void sesync::ClickSyncConverter::render_pulse(
    float *samples, const uint32_t frames, const uint32_t sample_rate,
    const uint64_t cycle_start_nsec, const uint64_t pulse_nsec,
    const uint64_t max_duration_nsec) const {
  const auto configured_duration =
      static_cast<uint64_t>(std::ceil(_config.pulse_length_ms * 1'000'000.0));
  const auto duration = std::min(configured_duration, max_duration_nsec);
  const auto pulse_end_nsec = pulse_nsec + duration;
  const auto cycle_end_nsec =
      cycle_start_nsec + frame_nsec(frames, sample_rate);
  if (pulse_end_nsec <= cycle_start_nsec || pulse_nsec >= cycle_end_nsec)
    return;

  const auto to_frame = [sample_rate](const uint64_t nsec) {
    return (nsec * sample_rate + 999'999'999ull) / 1'000'000'000ull;
  };
  const auto start = pulse_nsec <= cycle_start_nsec
                         ? uint64_t{0}
                         : to_frame(pulse_nsec - cycle_start_nsec);
  const auto end = pulse_end_nsec <= cycle_start_nsec
                       ? uint64_t{0}
                       : to_frame(pulse_end_nsec - cycle_start_nsec);
  const auto clamped_start = std::min<uint64_t>(start, frames);
  const auto clamped_end = std::min<uint64_t>(end, frames);
  std::fill(samples + clamped_start, samples + clamped_end,
            _config.pulse_amplitude);
}

std::string sesync::ClickSyncConverter::node_name(const OutputKind kind) const {
  const auto suffix = kind == OutputKind::Click
                          ? "click"
                          : kind == OutputKind::Reset ? "reset" : "run";
  return std::format("se.click_sync.{}.{}", _config.id, suffix);
}

std::string
sesync::ClickSyncConverter::description(const OutputKind kind) const {
  const auto suffix = kind == OutputKind::Click
                          ? "click"
                          : kind == OutputKind::Reset ? "reset" : "run";
  return std::format("{} {}", _config.name, suffix);
}

void sesync::ClickSyncConverter::process_callback(void *data) {
  auto &output = *static_cast<Output *>(data);
  output.converter->process(output);
}
