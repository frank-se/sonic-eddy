#include "ClickSyncConverter.h"

#include "logging/log.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <format>
#include <limits>
#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <pipewire/stream.h>
#include <spa/param/audio/format-utils.h>

namespace {

uint64_t frame_nsec(const uint32_t frames, const uint32_t sample_rate) {
  return (static_cast<uint64_t>(frames) * 1'000'000'000ull) / sample_rate;
}

} // namespace

class sesync::ClickSyncConverter::OutputProducer {
public:
  OutputProducer(pw_loop *loop, const OutputKind kind, ClickSyncConfig config,
                 std::shared_ptr<SyncClient> sync_client)
      : _loop(loop), _kind(kind), _config(std::move(config)),
        _sync_client(std::move(sync_client)) {}

  ~OutputProducer() { stop(); }

  bool start() {
    if (_stream != nullptr)
      return true;

    const auto name = node_name();
    const auto purpose = _kind == OutputKind::Click
                             ? "click-sync-click"
                             : _kind == OutputKind::Reset ? "click-sync-reset"
                                                          : "click-sync-run";
    auto *properties = pw_properties_new(
        PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
        PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Output/Audio",
        PW_KEY_NODE_NAME, name.c_str(), PW_KEY_NODE_DESCRIPTION,
        description().c_str(), "se.role", purpose, "pmx.purpose", purpose,
        "node.linger", "true", "node.passive", "false", nullptr);
    if (!_config.tag.empty())
      pw_properties_set(properties, "pmx.tag", _config.tag.c_str());

    static const pw_stream_events events = {
        .version = PW_VERSION_STREAM_EVENTS,
        .process = process_callback,
    };
    _stream =
        pw_stream_new_simple(_loop, name.c_str(), properties, &events, this);
    if (_stream == nullptr) {
      logging::log<logging::LogLevel::Error>(
          "Failed to create click sync stream '{}'", name);
      return false;
    }

    std::array<uint8_t, 1024> pod_buffer{};
    auto builder =
        SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
    auto audio_info =
        SPA_AUDIO_INFO_RAW_INIT(.format = SPA_AUDIO_FORMAT_F32, .channels = 1,
                                .position = {SPA_AUDIO_CHANNEL_MONO});
    const spa_pod *params[] = {
        spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &audio_info)};
    const auto result = pw_stream_connect(
        _stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
        static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                     PW_STREAM_FLAG_RT_PROCESS),
        params, 1);
    if (result >= 0)
      return true;

    logging::log<logging::LogLevel::Error>(
        "Failed to connect click sync stream '{}': {}", name, result);
    stop();
    return false;
  }

  void stop() {
    if (_stream == nullptr)
      return;
    pw_stream_destroy(_stream);
    _stream = nullptr;
  }

private:
  pw_loop *_loop;
  OutputKind _kind;
  ClickSyncConfig _config;
  std::shared_ptr<SyncClient> _sync_client;
  pw_stream *_stream = nullptr;
  uint32_t _last_cycle_frames = 1024;

  void process() {
    if (_stream == nullptr)
      return;

    auto *pipewire_buffer = pw_stream_dequeue_buffer(_stream);
    if (pipewire_buffer == nullptr)
      return;

    auto *buffer = pipewire_buffer->buffer;
    if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
        buffer->datas[0].chunk == nullptr) {
      pw_stream_queue_buffer(_stream, pipewire_buffer);
      return;
    }

    auto *data = &buffer->datas[0];
    auto frames = pipewire_buffer->requested > 0
                      ? pipewire_buffer->requested
                      : _last_cycle_frames;
    frames = std::min(frames, data->maxsize / sizeof(float));
    _last_cycle_frames = frames;

    auto *samples = static_cast<float *>(data->data);
    std::fill_n(samples, frames, 0.0f);

    pw_time stream_time{};
    uint32_t sample_rate = 48'000;
    uint64_t cycle_start_nsec = 0;
    if (pw_stream_get_time_n(_stream, &stream_time, sizeof(stream_time)) >= 0) {
      if (stream_time.rate.denom > 0)
        sample_rate = stream_time.rate.denom;
      if (stream_time.now > 0) {
        const uint64_t delay_nsec =
            stream_time.delay > 0 && stream_time.rate.denom > 0
                ? static_cast<uint64_t>(stream_time.delay) *
                      1'000'000'000ull / stream_time.rate.denom
                : 0;
        cycle_start_nsec =
            static_cast<uint64_t>(stream_time.now) + delay_nsec;
      }
    }

    const auto snapshot = _sync_client ? _sync_client->snapshot() : nullptr;
    if (snapshot && cycle_start_nsec > 0) {
      if (_kind == OutputKind::Click)
        render_click(samples, frames, sample_rate, cycle_start_nsec, *snapshot);
      else if (_kind == OutputKind::Reset)
        render_reset(samples, frames, sample_rate, cycle_start_nsec, *snapshot);
      else
        render_run(samples, frames, sample_rate, cycle_start_nsec, *snapshot);
    }

    data->chunk->offset = 0;
    data->chunk->size = frames * sizeof(float);
    data->chunk->stride = sizeof(float);
    data->chunk->flags = 0;
    pw_stream_queue_buffer(_stream, pipewire_buffer);
  }

  void render_click(float *samples, const uint32_t frames,
                    const uint32_t sample_rate,
                    const uint64_t cycle_start_nsec,
                    const SyncSnapshot &snapshot) const {
    if (_config.pulses_per_quarter_note == 0)
      return;

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
      if (snapshot.transport_state_at(current->beat) == TransportState::Stopped)
        continue;

      const auto beat_interval = next->nsec - current->nsec;
      for (uint32_t pulse = 0; pulse < _config.pulses_per_quarter_note;
           ++pulse) {
        const auto pulse_nsec =
            current->nsec +
            (beat_interval * pulse) / _config.pulses_per_quarter_note;
        const auto next_pulse_nsec =
            current->nsec +
            (beat_interval * (pulse + 1)) / _config.pulses_per_quarter_note;
        if (pulse_nsec >= cycle_end_nsec)
          break;
        render_pulse(samples, frames, sample_rate, cycle_start_nsec, pulse_nsec,
                     (next_pulse_nsec - pulse_nsec) / 2);
      }
    }
  }

  void render_reset(float *samples, const uint32_t frames,
                    const uint32_t sample_rate,
                    const uint64_t cycle_start_nsec,
                    const SyncSnapshot &snapshot) const {
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
        const auto beat_entry = std::ranges::find(
            snapshot.beat_history, beat, &BeatScheduleEntry::beat);
        if (beat_entry != snapshot.beat_history.end() &&
            beat_entry->nsec < cycle_end_nsec)
          render_pulse(samples, frames, sample_rate, cycle_start_nsec,
                       beat_entry->nsec, std::numeric_limits<uint64_t>::max());
      }
      previous_state = state;
    }
  }

  void render_run(float *samples, const uint32_t frames,
                  const uint32_t sample_rate,
                  const uint64_t cycle_start_nsec,
                  const SyncSnapshot &snapshot) const {
    const auto cycle_end_nsec =
        cycle_start_nsec + frame_nsec(frames, sample_rate);

    std::optional<uint64_t> beat;
    for (const auto &entry : snapshot.beat_history) {
      if (entry.nsec <= cycle_start_nsec && (!beat || entry.beat > *beat))
        beat = entry.beat;
    }
    if (!beat) {
      if (snapshot.beat_history.empty()) return;
      const auto first = snapshot.beat_history.front().beat;
      beat = first > 0 ? first - 1 : 0;
    }

    const auto to_frame = [&](const uint64_t nsec) -> uint32_t {
      const uint64_t offset = nsec - cycle_start_nsec;
      const uint64_t f = (offset * sample_rate + uint64_t{999'999'999}) /
                         uint64_t{1'000'000'000};
      return static_cast<uint32_t>(std::min(f, static_cast<uint64_t>(frames)));
    };

    auto state = snapshot.transport_state_at(*beat);
    uint32_t pos = 0;

    for (const auto &entry : snapshot.beat_history) {
      if (entry.nsec <= cycle_start_nsec || entry.nsec >= cycle_end_nsec)
        continue;
      const auto new_state = snapshot.transport_state_at(entry.beat);
      if (new_state == state) continue;
      if (state != TransportState::Stopped)
        std::fill_n(samples + pos, to_frame(entry.nsec) - pos,
                    _config.pulse_amplitude);
      pos = to_frame(entry.nsec);
      state = new_state;
    }

    if (state != TransportState::Stopped)
      std::fill_n(samples + pos, frames - pos, _config.pulse_amplitude);
  }

  void render_pulse(float *samples, const uint32_t frames,
                    const uint32_t sample_rate,
                    const uint64_t cycle_start_nsec,
                    const uint64_t pulse_nsec,
                    const uint64_t max_duration_nsec) const {
    const auto configured_duration = static_cast<uint64_t>(
        std::ceil(_config.pulse_length_ms * 1'000'000.0));
    const auto pulse_end_nsec =
        pulse_nsec + std::min(configured_duration, max_duration_nsec);
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
    std::fill(samples + std::min<uint64_t>(start, frames),
              samples + std::min<uint64_t>(end, frames),
              _config.pulse_amplitude);
  }

  [[nodiscard]] std::string node_name() const {
    const auto suffix = _kind == OutputKind::Click
                            ? "click"
                            : _kind == OutputKind::Reset ? "reset" : "run";
    return std::format("se.click_sync.{}.{}", _config.id, suffix);
  }

  [[nodiscard]] std::string description() const {
    const auto suffix = _kind == OutputKind::Click
                            ? "click"
                            : _kind == OutputKind::Reset ? "reset" : "run";
    return std::format("{} {}", _config.name, suffix);
  }

  static void process_callback(void *data) {
    static_cast<OutputProducer *>(data)->process();
  }
};

sesync::ClickSyncConverter::ClickSyncConverter(
    pw_loop *loop, ClickSyncConfig config,
    std::shared_ptr<SyncClient> sync_client)
    : _loop(loop), _config(std::move(config)),
      _sync_client(std::move(sync_client)) {}

sesync::ClickSyncConverter::~ClickSyncConverter() { stop(); }

bool sesync::ClickSyncConverter::start() {
  if (_click_output || _reset_output || _run_output)
    return true;

  _click_output = std::make_unique<OutputProducer>(
      _loop, OutputKind::Click, _config, _sync_client);
  _reset_output = std::make_unique<OutputProducer>(
      _loop, OutputKind::Reset, _config, _sync_client);
  _run_output = std::make_unique<OutputProducer>(
      _loop, OutputKind::Run, _config, _sync_client);

  if (_click_output->start() && _reset_output->start() &&
      _run_output->start())
    return true;

  stop();
  return false;
}

void sesync::ClickSyncConverter::stop() {
  _click_output.reset();
  _reset_output.reset();
  _run_output.reset();
}
