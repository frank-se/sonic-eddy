#include "MidiSyncSender.h"

#include "logging/log.h"

#include <algorithm>
#include <array>
#include <ctime>
#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <spa/control/control.h>
#include <spa/param/format.h>
#include <spa/pod/builder.h>

namespace {

uint64_t timespec_to_nsec(const timespec &ts) {
  return static_cast<uint64_t>(ts.tv_sec) * 1'000'000'000ull +
         static_cast<uint64_t>(ts.tv_nsec);
}

const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = midi::MidiSyncSender::process_callback,
};

} // namespace

midi::MidiSyncSender::MidiSyncSender(pw_loop *loop,
                                     sesync::SyncClient &sync_client)
    : _loop(loop), _sync_client(sync_client) {}

midi::MidiSyncSender::~MidiSyncSender() { stop(); }

void midi::MidiSyncSender::start() {
  if (_stream != nullptr)
    return;

  logging::log<logging::LogLevel::Info>("Starting MIDI sync sender");
  setup_stream();
}

void midi::MidiSyncSender::stop() {
  if (_stream != nullptr) {
    logging::log<logging::LogLevel::Info>("Stopping MIDI sync sender");
    pw_stream_destroy(_stream);
    _stream = nullptr;
  }

  _last_transport_state = sesync::TransportState::Stopped;
  _last_emitted_tick.reset();
}

void midi::MidiSyncSender::setup_stream() {
  const auto properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Midi", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Output/Midi",
      PW_KEY_FORMAT_DSP, "8 bit raw midi", PW_KEY_NODE_NAME,
      "se.midi_sync", PW_KEY_NODE_DESCRIPTION, "Sonic Eddy MIDI sync",
      "se.role", "midi-sync", nullptr);

  _stream = pw_stream_new_simple(_loop, "se.midi_sync", properties,
                                 &stream_events, this);
  if (_stream == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create MIDI sync stream");
    return;
  }

  const spa_pod *params[1];
  std::array<uint8_t, 256> buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer.data(), buffer.size());

  spa_pod_frame frame{};
  spa_pod_builder_push_object(&builder, &frame, SPA_TYPE_OBJECT_Format,
                              SPA_PARAM_EnumFormat);
  spa_pod_builder_add(
      &builder, SPA_FORMAT_mediaType, SPA_POD_Id(SPA_MEDIA_TYPE_application),
      SPA_FORMAT_mediaSubtype, SPA_POD_Id(SPA_MEDIA_SUBTYPE_control), 0);
  params[0] =
      static_cast<const spa_pod *>(spa_pod_builder_pop(&builder, &frame));

  const auto result = pw_stream_connect(
      _stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                   PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "Failed to connect MIDI sync stream: {}", result);
  }
}

void midi::MidiSyncSender::process() {
  if (_stream == nullptr)
    return;

  const auto pipewire_buffer = pw_stream_dequeue_buffer(_stream);
  if (pipewire_buffer == nullptr)
    return;

  const auto buffer = pipewire_buffer->buffer;
  if (buffer->n_datas != 1 || buffer->datas[0].data == nullptr) {
    pw_stream_queue_buffer(_stream, pipewire_buffer);
    return;
  }

  auto *spa_data = &buffer->datas[0];
  spa_data->chunk->offset = 0;
  spa_data->chunk->size = 0;
  spa_data->chunk->stride = 1;
  spa_data->chunk->flags = 0;

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, spa_data->data, spa_data->maxsize);

  spa_pod_frame sequence_frame{};
  spa_pod_builder_push_sequence(&builder, &sequence_frame, 0);
  emit_due_messages(builder, now_nsec());
  spa_pod_builder_pop(&builder, &sequence_frame);

  spa_data->chunk->size = builder.state.offset;
  pw_stream_queue_buffer(_stream, pipewire_buffer);
}

void midi::MidiSyncSender::emit_due_messages(spa_pod_builder &builder,
                                             const uint64_t now_nsec) {
  const auto snapshot = _sync_client.snapshot();
  if (!snapshot)
    return;

  const auto current_beat = snapshot->current_beat(now_nsec);
  if (!current_beat)
    return;

  const auto state = snapshot->transport_state_at(current_beat->beat);
  if (state == sesync::TransportState::Stopped) {
    if (_last_transport_state != sesync::TransportState::Stopped)
      emit_byte(builder, 0xfc);
    _last_transport_state = state;
    _last_emitted_tick.reset();
    return;
  }

  if (_last_transport_state == sesync::TransportState::Stopped) {
    emit_byte(builder, 0xfa);
    const auto first_tick = current_beat->beat * PulsesPerBeat;
    _last_emitted_tick = first_tick == 0 ? std::nullopt
                                         : std::optional(first_tick - 1);
  }
  _last_transport_state = state;

  auto next_tick =
      _last_emitted_tick ? *_last_emitted_tick + 1
                         : current_beat->beat * PulsesPerBeat;

  for (uint32_t emitted = 0; emitted < 256; ++emitted, ++next_tick) {
    const auto target_nsec = tick_nsec(*snapshot, next_tick);
    if (!target_nsec || *target_nsec > now_nsec)
      break;

    emit_byte(builder, 0xf8);
    _last_emitted_tick = next_tick;
  }
}

void midi::MidiSyncSender::emit_byte(spa_pod_builder &builder,
                                     const uint8_t byte) const {
  spa_pod_builder_control(&builder, 0, SPA_CONTROL_Midi);
  spa_pod_builder_bytes(&builder, &byte, 1);
}

uint64_t midi::MidiSyncSender::now_nsec() const {
  timespec ts{};
  clock_gettime(CLOCK_MONOTONIC, &ts);
  return timespec_to_nsec(ts);
}

std::optional<uint64_t>
midi::MidiSyncSender::tick_nsec(const sesync::SyncSnapshot &snapshot,
                                const uint64_t tick) const {
  const auto beat = tick / PulsesPerBeat;
  const auto pulse = tick % PulsesPerBeat;

  const auto &history = snapshot.beat_history;
  const auto current =
      std::ranges::find(history, beat, &sesync::BeatScheduleEntry::beat);
  if (current == history.end())
    return std::nullopt;

  if (pulse == 0)
    return current->nsec;

  const auto next =
      std::ranges::find(history, beat + 1, &sesync::BeatScheduleEntry::beat);
  if (next == history.end() || next->nsec <= current->nsec)
    return std::nullopt;

  return current->nsec +
         ((next->nsec - current->nsec) * pulse) / PulsesPerBeat;
}

void midi::MidiSyncSender::process_callback(void *data) {
  static_cast<MidiSyncSender *>(data)->process();
}
