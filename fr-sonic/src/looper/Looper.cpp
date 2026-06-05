#include "Looper.h"

#include "logging/log.h"
#include "sync/SyncClient.h"

#include <algorithm>
#include <array>
#include <charconv>
#include <chrono>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <filesystem>
#include <format>
#include <iomanip>
#include <limits>
#include <memory>
#include <optional>
#include <regex>
#include <sstream>
#include <string_view>
#include <thread>
#include <utility>

#include <FLAC/metadata.h>
#include <FLAC/stream_encoder.h>
#include <pipewire/keys.h>
#include <pipewire/properties.h>
#include <pipewire/stream.h>
#include <spa/param/audio/format-utils.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

namespace {

const pw_stream_events capture_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = looper::Looper::capture_param_changed_callback,
    .process = looper::Looper::capture_process_callback,
};

const pw_stream_events playback_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = looper::Looper::playback_param_changed_callback,
    .process = looper::Looper::playback_process_callback,
};

constexpr uint32_t PassthroughMaxFrames = 16384;
constexpr uint32_t ArchiveBitsPerSample = 24;

uint64_t monotonic_nsec() {
  timespec now{};
  clock_gettime(CLOCK_MONOTONIC, &now);
  return (static_cast<uint64_t>(now.tv_sec) * 1'000'000'000ull) +
         static_cast<uint64_t>(now.tv_nsec);
}

std::string pod_type_name(const uint32_t type) {
  switch (type) {
  case SPA_TYPE_String:
    return "String";
  case SPA_TYPE_Float:
    return "Float";
  case SPA_TYPE_Double:
    return "Double";
  case SPA_TYPE_Int:
    return "Int";
  case SPA_TYPE_Long:
    return "Long";
  case SPA_TYPE_Struct:
    return "Struct";
  default:
    return std::format("{}", type);
  }
}

std::optional<uint64_t> parse_u64(std::string_view text) {
  while (!text.empty() && text.front() == ' ')
    text.remove_prefix(1);
  while (!text.empty() && text.back() == ' ')
    text.remove_suffix(1);
  uint64_t value = 0;
  const auto result =
      std::from_chars(text.data(), text.data() + text.size(), value);
  if (result.ec != std::errc{} || result.ptr != text.data() + text.size())
    return std::nullopt;
  return value;
}

std::optional<looper::CommandEvent> parse_command_text(uint64_t scheduled_beat,
                                                       std::string_view text) {
  std::istringstream stream{std::string{text}};
  std::string command;
  stream >> command;
  if (command.empty())
    return std::nullopt;

  looper::CommandEvent event{.scheduled_beat = scheduled_beat};
  if (command == "play") {
    uint32_t loop_number = 0;
    if (!(stream >> loop_number))
      return std::nullopt;
    event.kind = looper::CommandKind::Play;
    event.loop_number = loop_number;
    return event;
  }

  if (command == "stop") {
    event.kind = looper::CommandKind::Stop;
    return event;
  }

  if (command == "archive") {
    uint32_t loop_number = 0;
    if (!(stream >> loop_number))
      return std::nullopt;
    event.kind = looper::CommandKind::Archive;
    event.loop_number = loop_number;
    return event;
  }

  if (command == "cut") {
    uint64_t first = 0;
    uint64_t second = 0;
    uint32_t loop_number = 0;
    if (!(stream >> first >> second))
      return std::nullopt;
    if (stream >> loop_number) {
      event.kind = looper::CommandKind::CutRange;
      event.start_beat = first;
      event.end_beat = second;
      event.loop_number = loop_number;
    } else {
      event.kind = looper::CommandKind::CutLength;
      event.loop_length = first;
      event.loop_number = static_cast<uint32_t>(second);
    }
    return event;
  }

  return std::nullopt;
}

uint32_t command_priority(const looper::CommandEvent &event) {
  switch (event.kind) {
  case looper::CommandKind::CutRange:
  case looper::CommandKind::CutLength:
    return 0;
  case looper::CommandKind::Play:
    return 1;
  case looper::CommandKind::Stop:
    return 2;
  case looper::CommandKind::Archive:
    return 3;
  }
  return 4;
}

std::string sanitize_path_part(std::string_view value) {
  std::string result;
  result.reserve(value.size());
  for (const auto character : value) {
    if ((character >= 'a' && character <= 'z') ||
        (character >= 'A' && character <= 'Z') ||
        (character >= '0' && character <= '9') || character == '-' ||
        character == '_') {
      result.push_back(character);
    } else {
      result.push_back('_');
    }
  }
  return result.empty() ? "looper" : result;
}

int32_t float_to_flac_sample(const float value) {
  const auto clamped = std::clamp(value, -1.0f, 1.0f);
  constexpr auto scale =
      static_cast<float>((int64_t{1} << (ArchiveBitsPerSample - 1)) - 1);
  return static_cast<int32_t>(std::lrint(clamped * scale));
}

bool add_vorbis_comment(FLAC__StreamMetadata *metadata, const char *name,
                        const std::string &value) {
  FLAC__StreamMetadata_VorbisComment_Entry entry{};
  if (!FLAC__metadata_object_vorbiscomment_entry_from_name_value_pair(
          &entry, name, value.c_str()))
    return false;
  if (FLAC__metadata_object_vorbiscomment_append_comment(metadata, entry,
                                                         /*copy=*/false))
    return true;
  std::free(entry.entry);
  return false;
}

} // namespace

looper::Looper::Looper(pw_loop *loop, LooperConfig config,
                       std::shared_ptr<sesync::SyncClient> sync_client)
    : _loop(loop), _config(std::move(config)),
      _sync_client(std::move(sync_client)),
      _mix(std::clamp(_config.mix, 0.0f, 1.0f)) {}

looper::Looper::~Looper() { stop(); }

bool looper::Looper::start() {
  if (_capture_stream != nullptr || _playback_stream != nullptr)
    return true;

  logging::log<logging::LogLevel::Info>("Starting looper '{}'", _config.name);
  _passthrough_channels = std::max(_config.channels, 1u);
  _passthrough_buffer.resize(PassthroughMaxFrames * _passthrough_channels);
  _passthrough_frames = 0;
  const auto rate = std::max(active_format().rate, 1u);
  _record_capacity_frames =
      static_cast<uint64_t>(rate) * std::max(_config.max_record_seconds, 1u);
  _record_buffer.assign(_record_capacity_frames * _passthrough_channels, 0.0f);
  _record_write_frame = 0;
  _recorded_frames = 0;
  _record_total_frames = 0;
  _recording = false;
  _transport_start_beat.reset();
  _ring_buffer_zero_beat.reset();
  _last_capture_frames = 0;
  start_copy_thread();

  if (!setup_capture_stream() || !setup_playback_stream()) {
    stop();
    return false;
  }

  return true;
}

void looper::Looper::stop() {
  destroy_streams();

  if (_capture_stream != nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' capture stream survived destroy_streams", _config.name);
  }

  if (_playback_stream != nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' playback stream survived destroy_streams", _config.name);
  }

  for (auto &slot : _loop_slots) {
    retire_samples(std::move(slot.samples));
    slot.samples.reset();
    slot.ready = false;
    slot.playing = false;
    slot.owned = false;
    slot.start_beat.reset();
    slot.end_beat.reset();
    slot.length_beats.reset();
    slot.play_started_beat.reset();
    slot.bpm.reset();
  }

  stop_copy_thread();
}

void looper::Looper::destroy_streams() {
  if (_capture_stream != nullptr) {
    pw_stream_destroy(_capture_stream);
    _capture_stream = nullptr;
  }

  if (_playback_stream != nullptr) {
    pw_stream_destroy(_playback_stream);
    _playback_stream = nullptr;
  }
}

void looper::Looper::start_copy_thread() {
  if (_copy_thread_running.exchange(true))
    return;

  _copy_thread = std::thread([this] { copy_thread_main(); });
}

void looper::Looper::stop_copy_thread() {
  if (!_copy_thread_running.exchange(false))
    return;

  _copy_wakeup.notify_one();
  if (_copy_thread.joinable())
    _copy_thread.join();
}

void looper::Looper::copy_thread_main() {
  logging::log<logging::LogLevel::Info>(
      "Looper '{}' background copy thread started", _config.name);

  while (_copy_thread_running.load(std::memory_order_acquire)) {
    drain_retired_sample_buffers();
    drain_state_updates();

    LoopCopyJob job{};
    if (!_copy_jobs.pop(job)) {
      LoopArchiveJob archive_job{};
      if (_archive_jobs.pop(archive_job)) {
        process_archive_job(archive_job);
        continue;
      }

      std::unique_lock lock{_copy_wakeup_mutex};
      _copy_wakeup.wait_for(lock, std::chrono::milliseconds(20), [this] {
        return !_copy_thread_running.load(std::memory_order_acquire) ||
               !_copy_jobs.empty() || !_archive_jobs.empty() ||
               !_state_updates.empty() || !_retired_sample_buffers.empty();
      });
      continue;
    }

    const auto started = monotonic_nsec();
    auto samples = std::make_shared<std::vector<float>>();
    samples->resize(job.length_frames * job.channels);
    for (uint64_t frame = 0; frame < job.length_frames; ++frame) {
      const auto source_frame =
          (job.ring_start_frame + frame) % _record_capacity_frames;
      const auto source_offset = source_frame * job.channels;
      const auto dest_offset = frame * job.channels;
      std::copy_n(_record_buffer.data() + source_offset, job.channels,
                  samples->data() + dest_offset);
    }

    double sum_squares = 0.0;
    float peak = 0.0f;
    float min = 0.0f;
    float max = 0.0f;
    if (!samples->empty()) {
      min = samples->front();
      max = samples->front();
      for (const auto sample : *samples) {
        sum_squares +=
            static_cast<double>(sample) * static_cast<double>(sample);
        peak = std::max(peak, std::abs(sample));
        min = std::min(min, sample);
        max = std::max(max, sample);
      }
    }
    const auto rms =
        samples->empty()
            ? 0.0
            : std::sqrt(sum_squares / static_cast<double>(samples->size()));

    const auto elapsed_usec = (monotonic_nsec() - started) / 1000;
    LoopCopyResult result{
        .loop_number = job.loop_number,
        .generation = job.generation,
        .length_frames = job.length_frames,
        .channels = job.channels,
        .elapsed_usec = elapsed_usec,
        .rms = rms,
        .peak = peak,
        .min = min,
        .max = max,
        .samples = std::move(samples),
    };
    if (!_copy_results.push(std::move(result))) {
      logging::log<logging::LogLevel::Error>(
          "Looper '{}' copy result queue full; dropped loop={} generation={}",
          _config.name, job.loop_number, job.generation);
    }

    LoopArchiveJob archive_job{};
    while (_archive_jobs.pop(archive_job))
      process_archive_job(archive_job);
  }

  drain_retired_sample_buffers();
  drain_state_updates();

  logging::log<logging::LogLevel::Info>(
      "Looper '{}' background copy thread stopped", _config.name);
}

void looper::Looper::process_archive_job(const LoopArchiveJob &job) {
  const auto started = monotonic_nsec();
  LoopArchiveResult result{
      .loop_number = job.loop_number,
      .generation = job.generation,
      .length_frames = job.length_frames,
      .channels = job.channels,
  };

  if (!_config.archive_folder_path) {
    result.error = "archive folder path is not configured";
    _archive_results.push(std::move(result));
    return;
  }

  std::vector<float> samples;
  samples.resize(job.length_frames * job.channels);
  if (job.samples) {
    if (job.samples->size() < samples.size()) {
      result.error = "owned sample buffer is shorter than loop metadata";
      _archive_results.push(std::move(result));
      return;
    }
    std::copy_n(job.samples->data(), samples.size(), samples.data());
  } else {
    for (uint64_t frame = 0; frame < job.length_frames; ++frame) {
      const auto source_frame =
          (job.ring_start_frame + frame) % _record_capacity_frames;
      const auto source_offset = source_frame * job.channels;
      const auto dest_offset = frame * job.channels;
      std::copy_n(_record_buffer.data() + source_offset, job.channels,
                  samples.data() + dest_offset);
    }
  }

  const auto path = archive_file_path(job);
  result.path = path;
  try {
    std::filesystem::create_directories(
        std::filesystem::path{path}.parent_path());
  } catch (const std::exception &exception) {
    result.error =
        std::format("failed to create archive directory: {}", exception.what());
    _archive_results.push(std::move(result));
    return;
  }

  FLAC__StreamEncoder *encoder = FLAC__stream_encoder_new();
  if (encoder == nullptr) {
    result.error = "failed to allocate FLAC encoder";
    _archive_results.push(std::move(result));
    return;
  }

  FLAC__stream_encoder_set_channels(encoder, job.channels);
  FLAC__stream_encoder_set_sample_rate(encoder, job.sample_rate);
  FLAC__stream_encoder_set_bits_per_sample(encoder, ArchiveBitsPerSample);
  FLAC__stream_encoder_set_compression_level(encoder, 5);
  FLAC__stream_encoder_set_total_samples_estimate(encoder, job.length_frames);

  auto *metadata =
      FLAC__metadata_object_new(FLAC__METADATA_TYPE_VORBIS_COMMENT);
  if (metadata != nullptr) {
    add_vorbis_comment(metadata, "SE_LOOPER_NAME", _config.name);
    add_vorbis_comment(metadata, "SE_LOOP_NUMBER",
                       std::format("{}", job.loop_number));
    add_vorbis_comment(metadata, "SE_LOOP_GENERATION",
                       std::format("{}", job.generation));
    add_vorbis_comment(metadata, "SE_LENGTH_FRAMES",
                       std::format("{}", job.length_frames));
    add_vorbis_comment(metadata, "SE_SAMPLE_RATE",
                       std::format("{}", job.sample_rate));
    add_vorbis_comment(metadata, "SE_CHANNELS",
                       std::format("{}", job.channels));
    if (job.start_beat)
      add_vorbis_comment(metadata, "SE_START_BEAT",
                         std::format("{}", *job.start_beat));
    if (job.end_beat)
      add_vorbis_comment(metadata, "SE_END_BEAT",
                         std::format("{}", *job.end_beat));
    if (job.length_beats)
      add_vorbis_comment(metadata, "SE_LENGTH_BEATS",
                         std::format("{}", *job.length_beats));
    if (job.bpm)
      add_vorbis_comment(metadata, "SE_BPM", std::format("{:.12g}", *job.bpm));
    FLAC__StreamMetadata *metadata_blocks[] = {metadata};
    FLAC__stream_encoder_set_metadata(encoder, metadata_blocks, 1);
  }

  const auto init_status =
      FLAC__stream_encoder_init_file(encoder, path.c_str(), nullptr, nullptr);
  if (init_status != FLAC__STREAM_ENCODER_INIT_STATUS_OK) {
    result.error =
        std::format("failed to initialize FLAC encoder: {}",
                    FLAC__StreamEncoderInitStatusString[init_status]);
    if (metadata != nullptr)
      FLAC__metadata_object_delete(metadata);
    FLAC__stream_encoder_delete(encoder);
    _archive_results.push(std::move(result));
    return;
  }

  std::vector<FLAC__int32> pcm;
  pcm.resize(samples.size());
  std::ranges::transform(samples, pcm.begin(), float_to_flac_sample);
  const auto write_ok = FLAC__stream_encoder_process_interleaved(
      encoder, pcm.data(), static_cast<uint32_t>(job.length_frames));
  const auto finish_ok = FLAC__stream_encoder_finish(encoder);
  if (metadata != nullptr)
    FLAC__metadata_object_delete(metadata);
  FLAC__stream_encoder_delete(encoder);

  if (!write_ok || !finish_ok) {
    result.error = "failed to write FLAC samples";
    _archive_results.push(std::move(result));
    return;
  }

  result.success = true;
  result.elapsed_usec = (monotonic_nsec() - started) / 1000;
  if (!_archive_results.push(std::move(result))) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' archive result queue full; dropped loop={} generation={}",
        _config.name, job.loop_number, job.generation);
  }
}

void looper::Looper::drain_retired_sample_buffers() {
  std::shared_ptr<std::vector<float>> retired;
  while (_retired_sample_buffers.pop(retired))
    retired.reset();
}

void looper::Looper::drain_state_updates() {
  LooperStateUpdate update{};
  bool has_update = false;
  while (_state_updates.pop(update))
    has_update = true;

  if (has_update)
    publish_state_update(format_state_json(update));
}

void looper::Looper::publish_state_update(std::string state_json) {
  struct PublishData {
    Looper *looper = nullptr;
    std::string state_json;
  };

  auto *data =
      new PublishData{.looper = this, .state_json = std::move(state_json)};
  pw_loop_invoke(
      _loop,
      [](spa_loop *loop, bool async, uint32_t seq, const void *invoke_data,
         size_t size, void *user_data) {
        (void)loop;
        (void)async;
        (void)seq;
        (void)invoke_data;
        (void)size;
        auto *data = static_cast<PublishData *>(user_data);
        data->looper->_published_state_json = std::move(data->state_json);
        data->looper->publish_params();
        delete data;
        return 0;
      },
      0, nullptr, 0, true, data);
}

bool looper::Looper::setup_capture_stream() {
  const auto name = capture_name();
  const auto description = std::format("{} capture", _config.description);
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Input/Audio",
      PW_KEY_NODE_NAME, name.c_str(), PW_KEY_NODE_DESCRIPTION,
      description.c_str(), "se.role", "looper-capture", "se.looper.name",
      _config.name.c_str(), "pmx.purpose", "looper-capture", "node.linger",
      "true", "node.passive", "false", nullptr);
  if (!_config.tag.empty())
    pw_properties_set(properties, "pmx.tag", _config.tag.c_str());
  if (_config.capture_target_object) {
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT,
                      _config.capture_target_object->c_str());
    pw_properties_set(properties, "node.autoconnect", "true");
    pw_properties_set(properties, "node.dont-fallback", "true");
  }

  _capture_stream = pw_stream_new_simple(_loop, name.c_str(), properties,
                                         &capture_stream_events, this);
  if (_capture_stream == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create looper capture stream '{}'", name);
    return false;
  }

  const spa_pod *params[1];
  std::array<uint8_t, 1024> buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer.data(), buffer.size());
  params[0] = build_audio_format(builder, _config.format, _config.channels);

  const auto result = pw_stream_connect(
      _capture_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
      stream_flags(_config.capture_target_object.has_value(),
                   PW_STREAM_FLAG_ASYNC),
      params, 1);
  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "Failed to connect looper capture stream '{}': {}", name, result);
    return false;
  }

  publish_params();
  return true;
}

bool looper::Looper::setup_playback_stream() {
  const auto name = playback_name();
  const auto description = std::format("{} playback", _config.description);
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Output/Audio",
      PW_KEY_NODE_NAME, name.c_str(), PW_KEY_NODE_DESCRIPTION,
      description.c_str(), "se.role", "looper-playback", "se.looper.name",
      _config.name.c_str(), "pmx.purpose", "looper-playback", "node.linger",
      "true", "node.passive", "true", nullptr);
  if (!_config.tag.empty())
    pw_properties_set(properties, "pmx.tag", _config.tag.c_str());
  if (_config.playback_target_object) {
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT,
                      _config.playback_target_object->c_str());
    pw_properties_set(properties, "node.autoconnect", "true");
    pw_properties_set(properties, "node.dont-fallback", "true");
  }

  _playback_stream = pw_stream_new_simple(_loop, name.c_str(), properties,
                                          &playback_stream_events, this);
  if (_playback_stream == nullptr) {
    logging::log<logging::LogLevel::Error>(
        "Failed to create looper playback stream '{}'", name);
    return false;
  }

  const spa_pod *params[1];
  std::array<uint8_t, 1024> buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, buffer.data(), buffer.size());
  const auto *channel_map = _config.playback_channel_map.empty()
                                ? nullptr
                                : &_config.playback_channel_map;
  params[0] = build_audio_format(builder, _config.format, _config.channels,
                                 channel_map);

  const auto result = pw_stream_connect(
      _playback_stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      stream_flags(_config.playback_target_object.has_value(),
                   PW_STREAM_FLAG_TRIGGER),
      params, 1);
  if (result < 0) {
    logging::log<logging::LogLevel::Error>(
        "Failed to connect looper playback stream '{}': {}", name, result);
    return false;
  }

  return true;
}

void looper::Looper::process() {
  process_playback();
}

void looper::Looper::process_capture() {
  if (_playback_stream == nullptr)
    return;

  const auto result = pw_stream_trigger_process(_playback_stream);
  if (result >= 0)
    return;

  while (_capture_stream != nullptr) {
    auto *capture_buffer = pw_stream_dequeue_buffer(_capture_stream);
    if (capture_buffer == nullptr)
      break;
    pw_stream_queue_buffer(_capture_stream, capture_buffer);
  }
}

void looper::Looper::process_playback() {
  drain_copy_results();
  drain_archive_results();
  drain_command_events();
  _last_capture_frames = 0;

  if (_playback_stream == nullptr)
    return;

  pw_buffer *capture_buffer = nullptr;
  if (_capture_stream != nullptr) {
    while (true) {
      auto *next_capture_buffer = pw_stream_dequeue_buffer(_capture_stream);
      if (next_capture_buffer == nullptr)
        break;
      if (capture_buffer != nullptr)
        pw_stream_queue_buffer(_capture_stream, capture_buffer);
      capture_buffer = next_capture_buffer;
    }
  }

  auto *playback_buffer = pw_stream_dequeue_buffer(_playback_stream);
  if (capture_buffer == nullptr || playback_buffer == nullptr)
    goto done;

  capture_passthrough_input(capture_buffer);
  write_passthrough_output(playback_buffer);

done:
  if (capture_buffer != nullptr)
    pw_stream_queue_buffer(_capture_stream, capture_buffer);
  if (playback_buffer != nullptr)
    pw_stream_queue_buffer(_playback_stream, playback_buffer);
}

void looper::Looper::drain_archive_results() {
  LoopArchiveResult result{};
  while (_archive_results.pop(result)) {
    if (result.success) {
      logging::log<logging::LogLevel::Info>(
          "Looper '{}' archived loop={} generation={} frames={} channels={} "
          "path='{}' in {}us",
          _config.name, result.loop_number, result.generation,
          result.length_frames, result.channels, result.path,
          result.elapsed_usec);
    } else {
      logging::log<logging::LogLevel::Error>(
          "Looper '{}' failed to archive loop={} generation={}: {}",
          _config.name, result.loop_number, result.generation, result.error);
    }
  }
}

void looper::Looper::drain_copy_results() {
  LoopCopyResult result{};
  while (_copy_results.pop(result)) {
    if (result.loop_number >= _loop_slots.size() || !result.samples)
      continue;

    auto &slot = _loop_slots[result.loop_number];
    if (slot.generation != result.generation) {
      logging::log<logging::LogLevel::Info>(
          "Looper '{}' ignored stale loop copy loop={} generation={} "
          "current={}",
          _config.name, result.loop_number, result.generation, slot.generation);
      continue;
    }

    retire_samples(std::move(slot.samples));
    slot.samples = std::move(result.samples);
    slot.length_frames = result.length_frames;
    slot.channels = result.channels;
    slot.rms = result.rms;
    slot.peak = result.peak;
    slot.min = result.min;
    slot.max = result.max;
    slot.owned = true;
    enqueue_state_update();
    logging::log<logging::LogLevel::Info>(
        "Looper '{}' copied loop={} generation={} frames={} channels={} in "
        "{}us rms={} peak={} min={} max={}",
        _config.name, result.loop_number, result.generation,
        result.length_frames, result.channels, result.elapsed_usec, result.rms,
        result.peak, result.min, result.max);
  }
}

void looper::Looper::drain_command_events() {
  CommandEvent event{};
  while (_command_events.pop(event))
    queue_pending_command(event);

  std::ranges::stable_sort(_pending_commands.begin(),
                           _pending_commands.begin() + _pending_command_count,
                           {}, [](const CommandEvent &event) {
                             return std::pair{event.scheduled_beat,
                                              command_priority(event)};
                           });
}

void looper::Looper::queue_pending_command(const CommandEvent &event) {
  if (_pending_command_count >= _pending_commands.size()) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' pending command queue full; dropped command kind={} "
        "beat={} loop={}",
        _config.name, static_cast<uint32_t>(event.kind), event.scheduled_beat,
        event.loop_number);
    return;
  }

  _pending_commands[_pending_command_count++] = event;
}

void looper::Looper::process_due_commands(const uint32_t frame_offset,
                                          const uint64_t buffer_start_nsec,
                                          const uint32_t rate) {
  if (_pending_command_count == 0)
    return;

  size_t remaining_count = 0;
  for (size_t index = 0; index < _pending_command_count; ++index) {
    const auto &queued_event = _pending_commands[index];
    const auto scheduled_frame =
        command_frame_offset(queued_event, buffer_start_nsec, rate);
    if (scheduled_frame && *scheduled_frame <= frame_offset) {
      if (command_waiting_for_recording(queued_event, rate)) {
        _pending_commands[remaining_count++] = queued_event;
        continue;
      }

      const auto deferred_cut = std::ranges::any_of(
          _pending_commands.begin(),
          _pending_commands.begin() + remaining_count,
          [&queued_event](const CommandEvent &event) {
            return event.scheduled_beat == queued_event.scheduled_beat &&
                   event.loop_number == queued_event.loop_number &&
                   (event.kind == CommandKind::CutLength ||
                    event.kind == CommandKind::CutRange);
          });
      if (deferred_cut) {
        _pending_commands[remaining_count++] = queued_event;
        continue;
      }

      _active_command_event = queued_event;
      _active_command_late_frames =
          static_cast<uint64_t>(static_cast<int64_t>(frame_offset) -
                                *scheduled_frame);
      ++_processed_command_count;
      apply_command_event(queued_event);
      _active_command_event.reset();
      _active_command_late_frames = 0;
    } else {
      _pending_commands[remaining_count++] = queued_event;
    }
  }
  _pending_command_count = remaining_count;
}

bool looper::Looper::command_waiting_for_recording(const CommandEvent &event,
                                                   const uint32_t rate) const {
  if (event.kind != CommandKind::CutLength &&
      event.kind != CommandKind::CutRange)
    return false;

  if (event.scheduled_beat == 0 || event.loop_number >= _loop_slots.size())
    return false;

  uint64_t start_beat = event.start_beat;
  uint64_t end_beat = event.end_beat;
  if (event.kind == CommandKind::CutLength) {
    if (event.loop_length == 0 || event.scheduled_beat < event.loop_length)
      return false;

    start_beat = event.scheduled_beat - event.loop_length;
    end_beat = event.scheduled_beat - 1;
  }

  if (start_beat > end_beat || end_beat == std::numeric_limits<uint64_t>::max())
    return false;

  const auto snapshot = _sync_client ? _sync_client->snapshot() : nullptr;
  if (!snapshot || !_ring_buffer_zero_beat)
    return false;

  const auto start_frame_abs =
      beat_frame_from_ring_zero(*snapshot, start_beat, rate);
  const auto end_frame_abs =
      beat_frame_from_ring_zero(*snapshot, end_beat + 1, rate);
  if (!start_frame_abs || !end_frame_abs || *end_frame_abs <= *start_frame_abs)
    return false;

  const auto length_frames = *end_frame_abs - *start_frame_abs;
  if (length_frames == 0 || length_frames > _record_capacity_frames)
    return false;

  const auto earliest_available = _record_total_frames > _recorded_frames
                                      ? _record_total_frames - _recorded_frames
                                      : uint64_t{0};
  if (*start_frame_abs < earliest_available)
    return false;

  return *end_frame_abs > _record_total_frames;
}

void looper::Looper::apply_command_event(const CommandEvent &event) {
  logging::log<logging::LogLevel::Info>(
      "Looper '{}' processed command event kind={} beat={} loop={}",
      _config.name, static_cast<uint32_t>(event.kind), event.scheduled_beat,
      event.loop_number);

  switch (event.kind) {
  case CommandKind::CutLength:
    cut_length(event.loop_length, event.loop_number);
    break;
  case CommandKind::CutRange:
    cut_range(event.start_beat, event.end_beat, event.loop_number);
    break;
  case CommandKind::Play:
    play_loop(event.loop_number);
    break;
  case CommandKind::Stop:
    stop_loops();
    break;
  case CommandKind::Archive:
    archive_loop(event.loop_number);
    break;
  }
}

void looper::Looper::cut_length(const uint64_t length_beats,
                                const uint32_t loop_number) {
  if (!_active_command_event || _active_command_event->scheduled_beat == 0) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut length without scheduled beat", _config.name);
    return;
  }
  if (length_beats == 0) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected zero-length cut for loop {}", _config.name,
        loop_number);
    return;
  }
  if (_active_command_event->scheduled_beat < length_beats) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut length={} at beat={} for loop {}",
        _config.name, length_beats, _active_command_event->scheduled_beat,
        loop_number);
    return;
  }

  cut_range(_active_command_event->scheduled_beat - length_beats,
            _active_command_event->scheduled_beat - 1, loop_number);
}

void looper::Looper::cut_range(const uint64_t start_beat,
                               const uint64_t end_beat,
                               const uint32_t loop_number) {
  if (loop_number >= _loop_slots.size()) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut for invalid loop {}", _config.name,
        loop_number);
    return;
  }
  if (start_beat > end_beat) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut range {}..{} for loop {}", _config.name,
        start_beat, end_beat, loop_number);
    return;
  }
  if (end_beat == std::numeric_limits<uint64_t>::max()) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut range ending at max beat for loop {}",
        _config.name, loop_number);
    return;
  }

  const auto snapshot = _sync_client ? _sync_client->snapshot() : nullptr;
  if (!snapshot || !_ring_buffer_zero_beat) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut range {}..{} loop={} without sync recording "
        "alignment",
        _config.name, start_beat, end_beat, loop_number);
    return;
  }

  const auto channels = _passthrough_channels;
  const auto rate = std::max(active_format().rate, 1u);
  const auto start_frame_abs =
      beat_frame_from_ring_zero(*snapshot, start_beat, rate);
  const auto end_frame_abs =
      beat_frame_from_ring_zero(*snapshot, end_beat + 1, rate);
  if (!start_frame_abs || !end_frame_abs ||
      *end_frame_abs <= *start_frame_abs) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut range {}..{} loop={} without beat timing",
        _config.name, start_beat, end_beat, loop_number);
    return;
  }

  const auto length_frames = *end_frame_abs - *start_frame_abs;
  logging::log<logging::LogLevel::Info>(
      "Looper '{}' attempting cut beats={}..{} loop={} recorded_frames={} "
      "required_frames={} total_recorded_frames={}",
      _config.name, start_beat, end_beat, loop_number, _recorded_frames,
      length_frames, _record_total_frames);

  const auto earliest_available = _record_total_frames > _recorded_frames
                                      ? _record_total_frames - _recorded_frames
                                      : uint64_t{0};
  if (length_frames == 0 || length_frames > _record_capacity_frames ||
      *start_frame_abs < earliest_available ||
      *end_frame_abs > _record_total_frames) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected cut beats={}..{} loop={} available_abs={}..{} "
        "required_abs={}..{}",
        _config.name, start_beat, end_beat, loop_number, earliest_available,
        _record_total_frames, *start_frame_abs, *end_frame_abs);
    return;
  }

  auto &slot = _loop_slots[loop_number];
  retire_samples(std::move(slot.samples));
  slot.samples.reset();
  slot.length_frames = length_frames;
  slot.playhead_frame = 0;
  slot.ring_start_frame = *start_frame_abs % _record_capacity_frames;
  slot.sample_rate = rate;
  slot.channels = channels;
  slot.ready = true;
  slot.playing = false;
  slot.owned = false;
  slot.start_beat = start_beat;
  slot.end_beat = end_beat;
  slot.length_beats = end_beat - start_beat + 1;
  slot.play_started_beat.reset();
  slot.bpm.reset();
  slot.bpm = _active_command_event && _active_command_event->scheduled_beat > 0
                 ? bpm_at_beat(_active_command_event->scheduled_beat)
                 : bpm_at_beat(start_beat);
  slot.rms = 0.0;
  slot.peak = 0.0f;
  slot.min = 0.0f;
  slot.max = 0.0f;
  ++slot.generation;

  enqueue_copy_job(slot, loop_number);
  enqueue_state_update();

  logging::log<logging::LogLevel::Info>(
      "Looper '{}' cut loop={} generation={} beats={}..{} length_frames={} "
      "channels={} source=ring",
      _config.name, loop_number, slot.generation, start_beat, end_beat,
      slot.length_frames, slot.channels);
}

void looper::Looper::enqueue_copy_job(const LoopSlot &slot,
                                      const uint32_t loop_number) {
  LoopCopyJob job{
      .loop_number = loop_number,
      .generation = slot.generation,
      .ring_start_frame = slot.ring_start_frame,
      .length_frames = slot.length_frames,
      .channels = slot.channels,
  };
  if (_copy_jobs.push(job)) {
    _copy_wakeup.notify_one();
    return;
  }

  logging::log<logging::LogLevel::Error>(
      "Looper '{}' copy job queue full; loop={} generation={} remains "
      "ring-backed",
      _config.name, loop_number, slot.generation);
}

void looper::Looper::archive_loop(const uint32_t loop_number) {
  if (!_config.archive_folder_path) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected archive for loop {} without archive folder path",
        _config.name, loop_number);
    return;
  }
  if (loop_number >= _loop_slots.size()) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected archive for invalid loop {}", _config.name,
        loop_number);
    return;
  }

  auto &slot = _loop_slots[loop_number];
  if (!slot.ready || slot.length_frames == 0) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected archive for empty loop {}", _config.name,
        loop_number);
    return;
  }
  if (slot.playing) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected archive for playing loop {}", _config.name,
        loop_number);
    return;
  }
  if (slot.owned && (!slot.samples || slot.samples->empty())) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected archive for incomplete owned loop {}",
        _config.name, loop_number);
    return;
  }

  LoopArchiveJob job{
      .loop_number = loop_number,
      .generation = slot.generation,
      .ring_start_frame = slot.ring_start_frame,
      .length_frames = slot.length_frames,
      .sample_rate = slot.sample_rate,
      .channels = slot.channels,
      .start_beat = slot.start_beat,
      .end_beat = slot.end_beat,
      .length_beats = slot.length_beats,
      .bpm = slot.bpm,
      .samples = slot.samples,
  };

  if (!_archive_jobs.push(job)) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' archive job queue full; rejected archive loop={} "
        "generation={}",
        _config.name, loop_number, slot.generation);
    return;
  }

  retire_samples(std::move(slot.samples));
  slot.samples.reset();
  slot.length_frames = 0;
  slot.playhead_frame = 0;
  slot.ring_start_frame = 0;
  slot.sample_rate = 0;
  slot.channels = 0;
  slot.ready = false;
  slot.playing = false;
  slot.owned = false;
  slot.start_beat.reset();
  slot.end_beat.reset();
  slot.length_beats.reset();
  slot.play_started_beat.reset();
  slot.bpm.reset();
  slot.rms = 0.0;
  slot.peak = 0.0f;
  slot.min = 0.0f;
  slot.max = 0.0f;
  ++slot.generation;
  _copy_wakeup.notify_one();
  enqueue_state_update();

  logging::log<logging::LogLevel::Info>(
      "Looper '{}' queued archive loop={} generation={} frames={} channels={}",
      _config.name, loop_number, job.generation, job.length_frames,
      job.channels);
}

void looper::Looper::retire_samples(
    std::shared_ptr<std::vector<float>> samples) {
  if (!samples)
    return;

  if (_retired_sample_buffers.push(std::move(samples))) {
    _copy_wakeup.notify_one();
    return;
  }

  logging::log<logging::LogLevel::Error>(
      "Looper '{}' retired sample buffer queue full; freeing on process path",
      _config.name);
}

void looper::Looper::play_loop(const uint32_t loop_number) {
  if (loop_number >= _loop_slots.size()) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected play for invalid loop {}", _config.name,
        loop_number);
    return;
  }

  auto &slot = _loop_slots[loop_number];
  if (!slot.ready || slot.length_frames == 0 ||
      (slot.owned && (!slot.samples || slot.samples->empty()))) {
    logging::log<logging::LogLevel::Error>(
        "Looper '{}' rejected play for unready loop {}", _config.name,
        loop_number);
    return;
  }

  for (auto &loop_slot : _loop_slots) {
    loop_slot.playing = false;
    loop_slot.play_started_beat.reset();
  }

  slot.playing = true;
  slot.playhead_frame = _active_command_late_frames % slot.length_frames;
  slot.play_started_beat =
      _active_command_event && _active_command_event->scheduled_beat > 0
          ? std::optional<uint64_t>{_active_command_event->scheduled_beat}
          : std::nullopt;
  enqueue_state_update();
  logging::log<logging::LogLevel::Info>(
      "Looper '{}' playing loop={} generation={}", _config.name, loop_number,
      slot.generation);
}

void looper::Looper::stop_loops() {
  bool stopped_any = false;
  for (auto &slot : _loop_slots)
    if (slot.playing) {
      stopped_any = true;
      slot.play_started_beat.reset();
    }
  for (auto &slot : _loop_slots)
    slot.playing = false;
  if (!stopped_any)
    return;

  enqueue_state_update();
  logging::log<logging::LogLevel::Info>("Looper '{}' stopped loops",
                                        _config.name);
}

float looper::Looper::render_wet_sample(const uint32_t channel) {
  for (auto &slot : _loop_slots) {
    if (!slot.playing || !slot.ready || slot.length_frames == 0 ||
        slot.channels == 0)
      continue;

    const auto slot_channel = std::min<uint32_t>(channel, slot.channels - 1);
    if (slot.owned && slot.samples) {
      return (
          *slot.samples)[(slot.playhead_frame * slot.channels) + slot_channel];
    } else if (!_record_buffer.empty()) {
      const auto ring_frame = (slot.ring_start_frame + slot.playhead_frame) %
                              _record_capacity_frames;
      return _record_buffer[(ring_frame * slot.channels) + slot_channel];
    }
  }
  return 0.0f;
}

void looper::Looper::publish_params() {
  if (_capture_stream == nullptr)
    return;

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, _params_buffer.data(), _params_buffer.size());

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);
  spa_pod_builder_string(&builder, "mix");
  spa_pod_builder_float(&builder, _mix.load(std::memory_order_relaxed));
  spa_pod_builder_string(&builder, "commands");
  spa_pod_builder_string(&builder, "[]");
  spa_pod_builder_string(&builder, "looper.state");
  spa_pod_builder_string(&builder, _published_state_json.c_str());
  spa_pod_builder_pop(&builder, &struct_frame);

  const spa_pod *params[] = {
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame))};
  pw_stream_update_params(_capture_stream, params, 1);
}

void looper::Looper::capture_passthrough_input(pw_buffer *capture_buffer) {
  auto *buffer = capture_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    _passthrough_frames = 0;
    _last_capture_frames = 0;
    return;
  }

  const auto *data = &buffer->datas[0];
  const auto channels = std::max(active_format().channels, 1u);
  const auto bytes_per_frame = channels * sizeof(float);
  const auto frames =
      std::min(static_cast<uint32_t>(data->chunk->size / bytes_per_frame),
               PassthroughMaxFrames);
  const auto sample_count = frames * channels;
  const auto *samples = static_cast<const float *>(data->data);
  const auto offset_samples = data->chunk->offset / sizeof(float);

  if (channels == _passthrough_channels &&
      sample_count <= _passthrough_buffer.size()) {
    std::copy_n(samples + offset_samples, sample_count,
                _passthrough_buffer.data());
  } else {
    _passthrough_frames = 0;
    _last_capture_frames = 0;
    return;
  }

  _passthrough_frames = frames;
  _last_capture_frames = frames;

  pw_time stream_time{};
  auto buffer_start_nsec = monotonic_nsec();
  if (_capture_stream != nullptr &&
      pw_stream_get_time_n(_capture_stream, &stream_time,
                           sizeof(stream_time)) >= 0 &&
      stream_time.now > 0) {
    buffer_start_nsec = static_cast<uint64_t>(stream_time.now);
  }

  const auto snapshot = _sync_client ? _sync_client->snapshot() : nullptr;
  const auto rate = std::max(active_format().rate, 1u);
  const auto nsec_per_frame = 1'000'000'000ull / static_cast<uint64_t>(rate);

  if (!_record_buffer.empty() && channels == _passthrough_channels &&
      snapshot) {
    for (uint32_t frame = 0; frame < frames; ++frame) {
      const auto frame_nsec =
          buffer_start_nsec + (static_cast<uint64_t>(frame) * nsec_per_frame);
      if (!should_record_frame(*snapshot, frame_nsec, rate))
        continue;

      const auto record_frame = _record_write_frame % _record_capacity_frames;
      const auto record_offset = record_frame * channels;
      const auto source_offset = frame * channels;
      std::copy_n(_passthrough_buffer.data() + source_offset, channels,
                  _record_buffer.data() + record_offset);
      _record_write_frame = (_record_write_frame + 1) % _record_capacity_frames;
      ++_record_total_frames;
      _recorded_frames =
          std::min<uint64_t>(_recorded_frames + 1, _record_capacity_frames);
    }
  } else {
    stop_recording();
  }
}

bool looper::Looper::should_record_frame(const sesync::SyncSnapshot &snapshot,
                                         const uint64_t frame_nsec,
                                         const uint32_t rate) {
  const auto current = snapshot.current_beat(frame_nsec);
  if (!current) {
    stop_recording();
    stop_loops();
    return false;
  }

  const auto transport = transport_state_entry_at(snapshot, current->beat);
  if (!transport || transport->state == sesync::TransportState::Stopped) {
    stop_recording();
    stop_loops();
    return false;
  }

  if (!_recording || _transport_start_beat != transport->beat) {
    const auto start_nsec = scheduled_beat_nsec(snapshot, transport->beat);
    uint64_t elapsed_frames = 0;
    if (start_nsec && frame_nsec > *start_nsec) {
      const auto elapsed_nsec = frame_nsec - *start_nsec;
      elapsed_frames =
          (elapsed_nsec * static_cast<uint64_t>(std::max(rate, 1u))) /
          1'000'000'000ull;
    }

    _record_write_frame = _record_capacity_frames == 0
                              ? 0
                              : elapsed_frames % _record_capacity_frames;
    _recorded_frames =
        std::min<uint64_t>(elapsed_frames, _record_capacity_frames);
    _record_total_frames = elapsed_frames;
    _recording = true;
    _transport_start_beat = transport->beat;
    _ring_buffer_zero_beat = transport->beat;
    enqueue_state_update();
    logging::log<logging::LogLevel::Info>(
        "Looper '{}' recording aligned to transport beat={} write_frame={}",
        _config.name, transport->beat, _record_write_frame);
  }

  return true;
}

void looper::Looper::stop_recording() {
  if (!_recording)
    return;

  _recording = false;
  enqueue_state_update();
  logging::log<logging::LogLevel::Info>("Looper '{}' recording stopped",
                                        _config.name);
}

void looper::Looper::write_passthrough_output(pw_buffer *playback_buffer) {
  auto *buffer = playback_buffer->buffer;
  if (buffer->n_datas > 0 && buffer->datas[0].data != nullptr &&
      buffer->datas[0].chunk != nullptr) {
    auto *data = &buffer->datas[0];
    const auto frames = playback_buffer->requested == 0
                            ? uint32_t{0}
                            : playback_buffer->requested;
    const auto channels = std::max(active_format().channels, 1u);
    const auto bytes_per_frame = channels * sizeof(float);
    const auto requested_size = static_cast<uint32_t>(frames * bytes_per_frame);
    const auto size = std::min<uint32_t>(data->maxsize, requested_size);
    const auto writable_frames = static_cast<uint32_t>(size / bytes_per_frame);
    auto *samples = static_cast<uint8_t *>(data->data);
    auto *output = reinterpret_cast<float *>(samples);
    const auto mix = _mix.load(std::memory_order_relaxed);
    const auto dry_gain = 1.0f - mix;
    const auto wet_gain = mix;
    const auto dry_available = _passthrough_channels == channels;
    pw_time stream_time{};
    auto buffer_start_nsec = monotonic_nsec();
    if (_playback_stream != nullptr &&
        pw_stream_get_time_n(_playback_stream, &stream_time,
                             sizeof(stream_time)) >= 0 &&
        stream_time.now > 0) {
      buffer_start_nsec = static_cast<uint64_t>(stream_time.now);
    }

    for (uint32_t frame = 0; frame < writable_frames; ++frame) {
      process_due_commands(frame, buffer_start_nsec,
                           std::max(active_format().rate, 1u));
      for (uint32_t channel = 0; channel < channels; ++channel) {
        const auto sample = (frame * channels) + channel;
        const auto dry_sample = dry_available && frame < _passthrough_frames
                                    ? _passthrough_buffer[sample]
                                    : 0.0f;
        output[sample] =
            (dry_sample * dry_gain) + (render_wet_sample(channel) * wet_gain);
      }

      for (auto &slot : _loop_slots) {
        if (slot.playing && slot.ready && slot.length_frames > 0)
          slot.playhead_frame = (slot.playhead_frame + 1) % slot.length_frames;
      }
    }

    data->chunk->offset = 0;
    data->chunk->size = size;
    data->chunk->stride = static_cast<int32_t>(bytes_per_frame);
  }
}

void looper::Looper::handle_capture_format(const uint32_t id,
                                           const spa_pod *param) {
  handle_params(id, param);
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  uint32_t media_type = 0;
  uint32_t media_subtype = 0;
  if (spa_format_parse(param, &media_type, &media_subtype) < 0)
    return;

  if (media_type != SPA_MEDIA_TYPE_audio ||
      media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  spa_format_audio_raw_parse(param, &_capture_format);
}

void looper::Looper::handle_playback_format(const uint32_t id,
                                            const spa_pod *param) {
  handle_params(id, param);
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  uint32_t media_type = 0;
  uint32_t media_subtype = 0;
  if (spa_format_parse(param, &media_type, &media_subtype) < 0)
    return;

  if (media_type != SPA_MEDIA_TYPE_audio ||
      media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  spa_format_audio_raw_parse(param, &_playback_format);
}

void looper::Looper::handle_params(const uint32_t id, const spa_pod *param) {
  if (param == nullptr || id != SPA_PARAM_Props)
    return;

  const auto params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr) {
    logging::log<logging::LogLevel::Warning>(
        "Looper '{}' received Props update without SPA_PROP_params",
        _config.name);
    return;
  }

  if (params_prop->value.type != SPA_TYPE_Struct) {
    logging::log<logging::LogLevel::Warning>(
        "Looper '{}' expected SPA_PROP_params Struct, got {}", _config.name,
        pod_type_name(params_prop->value.type));
    return;
  }

  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(static_cast<spa_pod *>(SPA_POD_BODY(&params_prop->value)),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key != nullptr) {
      handle_param_value(key, child);
    }
    ++index;
  }

  publish_params();
}

void looper::Looper::handle_param_value(const char *key, const spa_pod *value) {
  if (std::strcmp(key, "mix") == 0) {
    float mix = _mix.load(std::memory_order_relaxed);
    if (value->type == SPA_TYPE_Float) {
      spa_pod_get_float(value, &mix);
    } else if (value->type == SPA_TYPE_Double) {
      double double_mix = 0.0;
      spa_pod_get_double(value, &double_mix);
      mix = static_cast<float>(double_mix);
    } else if (value->type == SPA_TYPE_Int) {
      int32_t int_mix = 0;
      spa_pod_get_int(value, &int_mix);
      mix = static_cast<float>(int_mix);
    } else {
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored mix param with type {}", _config.name,
          pod_type_name(value->type));
      return;
    }

    _mix.store(std::clamp(mix, 0.0f, 1.0f), std::memory_order_relaxed);
    logging::log<logging::LogLevel::Info>("Looper '{}' mix set to {}",
                                          _config.name,
                                          _mix.load(std::memory_order_relaxed));
    return;
  }

  if (std::strcmp(key, "commands") == 0 || std::strcmp(key, "command") == 0) {
    if (value->type != SPA_TYPE_String) {
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored commands param with type {}", _config.name,
          pod_type_name(value->type));
      return;
    }
    const char *commands = nullptr;
    spa_pod_get_string(value, &commands);
    if (commands != nullptr)
      parse_commands_param(commands);
  }
}

void looper::Looper::enqueue_command(const CommandEvent &event) {
  if (_command_events.push(event)) {
    logging::log<logging::LogLevel::Info>(
        "Looper '{}' queued command event kind={} beat={} loop={}",
        _config.name, static_cast<uint32_t>(event.kind), event.scheduled_beat,
        event.loop_number);
    return;
  }

  ++_dropped_command_count;
  logging::log<logging::LogLevel::Error>(
      "Looper '{}' command event queue full; dropped command kind={} beat={} "
      "loop={} dropped_count={}",
      _config.name, static_cast<uint32_t>(event.kind), event.scheduled_beat,
      event.loop_number, _dropped_command_count);
}

void looper::Looper::parse_commands_param(const char *value) {
  const std::string_view text{value};
  static const std::regex tuple_regex{
      R"re(\[\s*([0-9]+)\s*,\s*"([^"]+)"\s*\])re"};

  bool parsed_tuple = false;
  std::vector<std::pair<uint64_t, std::string>> seen_commands;
  std::vector<CommandEvent> parsed_events;
  for (auto it = std::cregex_iterator(value, value + text.size(), tuple_regex);
       it != std::cregex_iterator{}; ++it) {
    parsed_tuple = true;
    const auto beat = parse_u64((*it)[1].str());
    if (!beat) {
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored command with invalid beat '{}'", _config.name,
          (*it)[1].str());
      continue;
    }
    const auto command_text = (*it)[2].str();
    const auto duplicate =
        std::ranges::any_of(seen_commands, [&](const auto &seen) {
          return seen.first == *beat && seen.second == command_text;
        });
    if (duplicate) {
      logging::log<logging::LogLevel::Error>(
          "Looper '{}' ignored duplicate command '{}' for beat {}",
          _config.name, command_text, *beat);
      continue;
    }
    seen_commands.emplace_back(*beat, command_text);

    auto event = parse_command_text(*beat, command_text);
    if (event)
      parsed_events.push_back(*event);
    else
      logging::log<logging::LogLevel::Warning>(
          "Looper '{}' ignored invalid command '{}'", _config.name,
          command_text);
  }

  if (parsed_tuple) {
    std::ranges::stable_sort(parsed_events, {}, [](const CommandEvent &event) {
      return std::pair{event.scheduled_beat, command_priority(event)};
    });
    for (const auto &event : parsed_events)
      enqueue_command(event);
    return;
  }

  auto event = parse_command_text(0, text);
  if (event)
    enqueue_command(*event);
  else
    logging::log<logging::LogLevel::Warning>(
        "Looper '{}' ignored invalid commands param '{}'", _config.name, value);
}

void looper::Looper::enqueue_state_update() {
  const auto update = capture_state_update();
  if (_state_updates.push(update)) {
    _copy_wakeup.notify_one();
    return;
  }

  logging::log<logging::LogLevel::Error>(
      "Looper '{}' state update queue full; dropped state update",
      _config.name);
}

std::string looper::Looper::capture_name() const {
  return std::format("{}.capture", _config.name);
}

std::string looper::Looper::playback_name() const {
  return std::format("{}.playback", _config.name);
}

const spa_audio_info_raw &looper::Looper::active_format() const {
  if (_playback_format.channels != 0)
    return _playback_format;
  if (_capture_format.channels != 0)
    return _capture_format;

  static const spa_audio_info_raw fallback = {
      .format = SPA_AUDIO_FORMAT_F32,
      .flags = SPA_AUDIO_FLAG_NONE,
      .rate = 48000,
      .channels = 2,
  };
  return fallback;
}

pw_stream_flags looper::Looper::stream_flags(
    const bool autoconnect, const pw_stream_flags extra_flags) const {
  auto flags = static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                            PW_STREAM_FLAG_RT_PROCESS |
                                            extra_flags);
  if (autoconnect)
    flags = static_cast<pw_stream_flags>(flags | PW_STREAM_FLAG_AUTOCONNECT);
  return flags;
}

std::optional<uint64_t> looper::Looper::current_sync_beat() const {
  if (!_sync_client)
    return std::nullopt;

  const auto current = _sync_client->current_beat(monotonic_nsec());
  if (!current)
    return std::nullopt;

  return current->beat;
}

std::optional<double> looper::Looper::bpm_at_beat(const uint64_t beat) const {
  if (!_sync_client)
    return std::nullopt;

  const auto snapshot = _sync_client->snapshot();
  if (!snapshot)
    return std::nullopt;

  const auto bpm = snapshot->bpm_at(beat);
  if (bpm <= 0.0)
    return std::nullopt;

  return bpm;
}

std::optional<uint64_t>
looper::Looper::scheduled_beat_nsec(const sesync::SyncSnapshot &snapshot,
                                    const uint64_t beat) const {
  const auto find_beat =
      [beat](const auto &entries) -> std::optional<uint64_t> {
    for (const auto &entry : entries) {
      if (entry.beat == beat)
        return entry.nsec;
    }
    return std::nullopt;
  };

  if (const auto scheduled = find_beat(snapshot.beat_schedule))
    return scheduled;
  return find_beat(snapshot.beat_history);
}

std::optional<sesync::TransportStateEntry>
looper::Looper::transport_state_entry_at(const sesync::SyncSnapshot &snapshot,
                                         const uint64_t beat) const {
  std::optional<sesync::TransportStateEntry> current;
  for (const auto &entry : snapshot.transport_states) {
    if (entry.beat > beat)
      break;
    current = entry;
  }
  return current;
}

std::optional<uint64_t>
looper::Looper::beat_frame_from_ring_zero(const sesync::SyncSnapshot &snapshot,
                                          const uint64_t beat,
                                          const uint32_t rate) const {
  if (!_ring_buffer_zero_beat)
    return std::nullopt;

  const auto zero_nsec = scheduled_beat_nsec(snapshot, *_ring_buffer_zero_beat);
  const auto beat_nsec = scheduled_beat_nsec(snapshot, beat);
  if (!zero_nsec || !beat_nsec || *beat_nsec < *zero_nsec)
    return std::nullopt;

  const auto delta_nsec = *beat_nsec - *zero_nsec;
  return (delta_nsec * static_cast<uint64_t>(std::max(rate, 1u))) /
         1'000'000'000ull;
}

std::optional<int64_t>
looper::Looper::command_frame_offset(const CommandEvent &event,
                                     const uint64_t buffer_start_nsec,
                                     const uint32_t rate) const {
  if (event.scheduled_beat == 0)
    return 0;
  if (!_sync_client)
    return std::nullopt;

  const auto snapshot = _sync_client->snapshot();
  if (!snapshot)
    return std::nullopt;

  const auto beat_nsec = scheduled_beat_nsec(*snapshot, event.scheduled_beat);
  if (!beat_nsec)
    return std::nullopt;

  const auto delta_nsec = static_cast<int64_t>(*beat_nsec) -
                          static_cast<int64_t>(buffer_start_nsec);
  return (delta_nsec * static_cast<int64_t>(std::max(rate, 1u))) /
         1'000'000'000ll;
}

looper::LooperStateUpdate looper::Looper::capture_state_update() const {
  LooperStateUpdate update{};
  update.recording = _recording;
  if (_transport_start_beat) {
    update.transport_start_beat = *_transport_start_beat;
    update.has_transport_start_beat = true;
  }
  if (_ring_buffer_zero_beat) {
    update.ring_buffer_zero_beat = *_ring_buffer_zero_beat;
    update.has_ring_buffer_zero_beat = true;
  }
  for (size_t index = 0; index < _loop_slots.size(); ++index) {
    const auto &slot = _loop_slots[index];
    auto &entry = update.loops[index];
    entry.loop_number = static_cast<uint32_t>(index);
    if (!slot.ready)
      continue;

    entry.populated = true;
    entry.generation = slot.generation;
    entry.length_frames = slot.length_frames;
    entry.playhead_frame = slot.playhead_frame;
    entry.sample_rate = slot.sample_rate;
    entry.channels = slot.channels;
    entry.rms = slot.rms;
    entry.peak = slot.peak;
    entry.min = slot.min;
    entry.max = slot.max;
    entry.playing = slot.playing;
    entry.owned = slot.owned;
    if (slot.start_beat) {
      entry.start_beat = *slot.start_beat;
      entry.has_start_beat = true;
    }
    if (slot.end_beat) {
      entry.end_beat = *slot.end_beat;
      entry.has_end_beat = true;
    }
    if (slot.length_beats) {
      entry.length_beats = *slot.length_beats;
      entry.has_length_beats = true;
    }
    if (slot.play_started_beat) {
      entry.play_started_beat = *slot.play_started_beat;
      entry.has_play_started_beat = true;
    }
    if (slot.bpm) {
      entry.bpm = *slot.bpm;
      entry.has_bpm = true;
    }
  }
  return update;
}

std::string
looper::Looper::format_state_json(const LooperStateUpdate &update) const {
  std::ostringstream json;
  json << std::setprecision(12);

  auto append_optional_u64 = [&json](const bool has_value,
                                     const uint64_t value) {
    if (has_value)
      json << value;
    else
      json << "null";
  };
  auto append_optional_double = [&json](const bool has_value,
                                        const double value) {
    if (has_value)
      json << value;
    else
      json << "null";
  };

  const LoopStateEntry *active = nullptr;
  for (const auto &loop : update.loops) {
    if (loop.populated && loop.playing) {
      active = &loop;
      break;
    }
  }

  json << R"({"version":1,"active_loop":)";
  if (active != nullptr)
    json << active->loop_number;
  else
    json << "null";

  json << R"(,"loops":[)";
  bool first_loop = true;
  for (const auto &loop : update.loops) {
    if (!loop.populated)
      continue;
    if (!first_loop)
      json << ",";
    first_loop = false;

    json << R"({"loop_number":)" << loop.loop_number << R"(,"generation":)"
         << loop.generation << R"(,"state":")"
         << (loop.playing ? "playing" : "stopped") << R"(","source":")"
         << (loop.owned ? "owned" : "ring") << R"(","start_beat":)";
    append_optional_u64(loop.has_start_beat, loop.start_beat);
    json << R"(,"end_beat":)";
    append_optional_u64(loop.has_end_beat, loop.end_beat);
    json << R"(,"length_beats":)";
    append_optional_u64(loop.has_length_beats, loop.length_beats);
    json << R"(,"length_frames":)" << loop.length_frames << R"(,"sample_rate":)"
         << loop.sample_rate << R"(,"channels":)" << loop.channels
         << R"(,"rms":)" << loop.rms << R"(,"peak":)" << loop.peak
         << R"(,"min":)" << loop.min << R"(,"max":)" << loop.max
         << R"(,"bpm":)";
    append_optional_double(loop.has_bpm, loop.bpm);
    json << "}";
  }

  json << R"(],"recording":)" << (update.recording ? "true" : "false")
       << R"(,"transport_alignment":{"transport_start_beat":)";
  append_optional_u64(update.has_transport_start_beat,
                      update.transport_start_beat);
  json << R"(,"ring_buffer_zero_beat":)";
  append_optional_u64(update.has_ring_buffer_zero_beat,
                      update.ring_buffer_zero_beat);
  json << "}";

  json << R"(,"active_playback":)";
  if (active != nullptr) {
    json << R"({"loop_number":)" << active->loop_number << R"(,"generation":)"
         << active->generation << R"(,"started_at_beat":)";
    append_optional_u64(active->has_play_started_beat,
                        active->play_started_beat);
    json << R"(,"playhead_samples":)" << active->playhead_frame << "}";
  } else {
    json << "null";
  }

  json << R"(,"pending_jobs":[],"last_command_failure":null})";
  return json.str();
}

std::string looper::Looper::archive_file_path(const LoopArchiveJob &job) const {
  const auto folder =
      _config.archive_folder_path ? *_config.archive_folder_path : ".";
  const auto filename = std::format("{}-loop-{}-generation-{}.flac",
                                    sanitize_path_part(_config.name),
                                    job.loop_number, job.generation);
  return (std::filesystem::path{folder} / filename).string();
}

const spa_pod *looper::Looper::build_audio_format(
    spa_pod_builder &builder, spa_audio_format format, uint32_t channels,
    const std::vector<spa_audio_channel> *channel_map) {
  spa_audio_info_raw audio_info{};
  audio_info.format = format;
  audio_info.channels = channels;
  if (channel_map && !channel_map->empty()) {
    for (size_t i = 0; i < channel_map->size() && i < SPA_AUDIO_MAX_CHANNELS;
         ++i)
      audio_info.position[i] = static_cast<uint32_t>((*channel_map)[i]);
  }
  return spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat,
                                    &audio_info);
}

void looper::Looper::capture_process_callback(void *data) {
  static_cast<Looper *>(data)->process_capture();
}

void looper::Looper::playback_process_callback(void *data) {
  static_cast<Looper *>(data)->process_playback();
}

void looper::Looper::capture_param_changed_callback(void *data,
                                                    const uint32_t id,
                                                    const spa_pod *param) {
  static_cast<Looper *>(data)->handle_capture_format(id, param);
}

void looper::Looper::playback_param_changed_callback(void *data,
                                                     const uint32_t id,
                                                     const spa_pod *param) {
  static_cast<Looper *>(data)->handle_playback_format(id, param);
}
