#include <algorithm>
#include <array>
#include <cmath>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <iostream>
#include <limits>
#include <optional>
#include <sstream>
#include <string>

#include "sync/SyncClient.h"

#include <pipewire/context.h>
#include <pipewire/core.h>
#include <pipewire/keys.h>
#include <pipewire/main-loop.h>
#include <pipewire/pipewire.h>
#include <pipewire/proxy.h>
#include <pipewire/properties.h>
#include <pipewire/stream.h>
#include <spa/param/audio/format-utils.h>
#include <spa/pod/builder.h>
#include <spa/utils/dict.h>
#include <spa/utils/hook.h>

namespace {

constexpr double Pi = 3.14159265358979323846;

enum class Mode { Constant, Alternating, Sine, BeatAlternating };

struct Options {
  std::string name = "se.test_signal";
  std::optional<std::string> target;
  Mode mode = Mode::Alternating;
  float value = 0.7f;
  float high_value = 0.9f;
  double frequency = 60.0;
  uint32_t rate = 48000;
  uint32_t channels = 2;
  uint32_t duration_seconds = 0;
  bool json = false;
};

struct Stats {
  uint64_t frames = 0;
  double sum_sq = 0.0;
  float peak = 0.0f;
  float min = std::numeric_limits<float>::max();
  float max = std::numeric_limits<float>::lowest();
};

struct App {
  Options options;
  pw_main_loop *main_loop = nullptr;
  pw_context *context = nullptr;
  pw_core *core = nullptr;
  pw_registry *registry = nullptr;
  pw_stream *stream = nullptr;
  std::shared_ptr<sesync::SyncClient> sync_client;
  spa_hook registry_listener{};
  spa_hook stream_listener{};
  spa_audio_info_raw format{};
  Stats total{};
  Stats window{};
  uint64_t frame = 0;
  uint64_t next_report_frame = 0;
  bool node_identity_printed = false;
};

App *g_app = nullptr;

void print_usage(const char *argv0) {
  std::cerr << "Usage: " << argv0
            << " [-p target] [-n name] [-m constant|alternating|sine|beat-alternating]\n"
            << "       [--value v] [--high-value v] [-f hz] [-d seconds]\n"
            << "       [-r rate] [--channels n] [--json]\n";
}

bool parse_uint(const char *text, uint32_t &out) {
  char *end = nullptr;
  const auto value = std::strtoul(text, &end, 10);
  if (end == text || *end != '\0')
    return false;
  out = static_cast<uint32_t>(value);
  return true;
}

bool parse_float(const char *text, float &out) {
  char *end = nullptr;
  const auto value = std::strtof(text, &end);
  if (end == text || *end != '\0')
    return false;
  out = value;
  return true;
}

bool parse_double(const char *text, double &out) {
  char *end = nullptr;
  const auto value = std::strtod(text, &end);
  if (end == text || *end != '\0')
    return false;
  out = value;
  return true;
}

bool parse_mode(const char *text, Mode &out) {
  const std::string value = text;
  if (value == "constant") {
    out = Mode::Constant;
    return true;
  }
  if (value == "alternating") {
    out = Mode::Alternating;
    return true;
  }
  if (value == "sine") {
    out = Mode::Sine;
    return true;
  }
  if (value == "beat-alternating") {
    out = Mode::BeatAlternating;
    return true;
  }
  return false;
}

const char *mode_name(const Mode mode) {
  switch (mode) {
  case Mode::Constant:
    return "constant";
  case Mode::Alternating:
    return "alternating";
  case Mode::Sine:
    return "sine";
  case Mode::BeatAlternating:
    return "beat-alternating";
  }
  return "unknown";
}

std::string json_escape(const std::string_view value) {
  std::ostringstream escaped;
  for (const auto ch : value) {
    switch (ch) {
    case '"':
      escaped << "\\\"";
      break;
    case '\\':
      escaped << "\\\\";
      break;
    case '\n':
      escaped << "\\n";
      break;
    case '\r':
      escaped << "\\r";
      break;
    case '\t':
      escaped << "\\t";
      break;
    default:
      escaped << ch;
      break;
    }
  }
  return escaped.str();
}

bool parse_args(int argc, char **argv, Options &options) {
  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    auto require_value = [&](const char *name) -> const char * {
      if (i + 1 >= argc) {
        std::cerr << "Missing value for " << name << "\n";
        return nullptr;
      }
      return argv[++i];
    };

    if (arg == "-p" || arg == "--target") {
      if (const auto *value = require_value(arg.c_str()))
        options.target = value;
      else
        return false;
    } else if (arg == "-n" || arg == "--name") {
      if (const auto *value = require_value(arg.c_str()))
        options.name = value;
      else
        return false;
    } else if (arg == "-m" || arg == "--mode") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_mode(value, options.mode))
        return false;
    } else if (arg == "--value") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_float(value, options.value))
        return false;
    } else if (arg == "--high-value") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_float(value, options.high_value))
        return false;
    } else if (arg == "-f" || arg == "--frequency") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_double(value, options.frequency))
        return false;
    } else if (arg == "-d" || arg == "--duration") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_uint(value, options.duration_seconds))
        return false;
    } else if (arg == "-r" || arg == "--rate") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_uint(value, options.rate))
        return false;
    } else if (arg == "--channels") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_uint(value, options.channels))
        return false;
    } else if (arg == "--json") {
      options.json = true;
    } else if (arg == "-h" || arg == "--help") {
      print_usage(argv[0]);
      std::exit(0);
    } else {
      std::cerr << "Unknown argument: " << arg << "\n";
      return false;
    }
  }
  return true;
}

uint64_t monotonic_nsec() {
  timespec now{};
  clock_gettime(CLOCK_MONOTONIC, &now);
  return (static_cast<uint64_t>(now.tv_sec) * 1'000'000'000ull) +
         static_cast<uint64_t>(now.tv_nsec);
}

float sample_value(const App &app, uint64_t frame, uint64_t frame_nsec) {
  const auto rate = std::max(app.format.rate, 1u);
  switch (app.options.mode) {
  case Mode::Constant:
    return app.options.value;
  case Mode::Alternating:
    return ((frame / rate) % 2) == 0 ? app.options.value
                                     : app.options.high_value;
  case Mode::Sine:
    return static_cast<float>(
        app.options.value *
        std::sin(2.0 * Pi * app.options.frequency *
                 (static_cast<double>(frame) / static_cast<double>(rate))));
  case Mode::BeatAlternating: {
    if (!app.sync_client)
      return app.options.value;
    const auto current = app.sync_client->current_beat(frame_nsec);
    if (!current)
      return app.options.value;
    return (current->beat % 2) == 0 ? app.options.value
                                    : app.options.high_value;
  }
  }
  return 0.0f;
}

void add_sample(Stats &stats, const float sample) {
  const auto abs_sample = std::abs(sample);
  stats.sum_sq += static_cast<double>(sample) * static_cast<double>(sample);
  stats.peak = std::max(stats.peak, abs_sample);
  stats.min = std::min(stats.min, sample);
  stats.max = std::max(stats.max, sample);
}

void print_stats(const char *prefix, const Stats &stats, const bool json) {
  const auto rms =
      stats.frames == 0 ? 0.0 : std::sqrt(stats.sum_sq / stats.frames);
  const auto min = stats.frames == 0 ? 0.0f : stats.min;
  const auto max = stats.frames == 0 ? 0.0f : stats.max;
  if (json) {
    std::cout << "{\"type\":\"stats\",\"app\":\"signal\",\"scope\":\""
              << prefix << "\",\"frames\":" << stats.frames
              << ",\"rms\":" << rms << ",\"peak\":" << stats.peak
              << ",\"min\":" << min << ",\"max\":" << max << "}"
              << std::endl;
    return;
  }

  std::cout << prefix << " frames=" << stats.frames << " rms=" << rms
            << " peak=" << stats.peak << " min=" << min << " max=" << max
            << "\n";
}

void on_process(void *data) {
  auto *app = static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app->stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(app->stream, pw_buffer);
    return;
  }

  auto *spa_data = &buffer->datas[0];
  const auto channels = std::max(app->format.channels, 1u);
  const auto requested =
      pw_buffer->requested == 0 ? spa_data->maxsize / (channels * sizeof(float))
                                : pw_buffer->requested;
  const auto frames =
      std::min<uint32_t>(requested,
                         spa_data->maxsize / (channels * sizeof(float)));
  auto *samples = static_cast<float *>(spa_data->data);
  pw_time stream_time{};
  auto buffer_start_nsec = monotonic_nsec();
  if (pw_stream_get_time_n(app->stream, &stream_time, sizeof(stream_time)) >=
          0 &&
      stream_time.now > 0) {
    buffer_start_nsec = static_cast<uint64_t>(stream_time.now);
  }
  const auto nsec_per_frame =
      1'000'000'000ull / static_cast<uint64_t>(std::max(app->format.rate, 1u));

  for (uint32_t frame = 0; frame < frames; ++frame) {
    const auto value = sample_value(
        *app, app->frame + frame,
        buffer_start_nsec + (static_cast<uint64_t>(frame) * nsec_per_frame));
    add_sample(app->total, value);
    add_sample(app->window, value);
    for (uint32_t channel = 0; channel < channels; ++channel)
      samples[(frame * channels) + channel] = value;
  }

  app->frame += frames;
  app->total.frames += frames;
  app->window.frames += frames;

  const auto rate = std::max(app->format.rate, 1u);
  if (app->next_report_frame == 0)
    app->next_report_frame = rate;
  if (app->total.frames >= app->next_report_frame) {
    print_stats("window", app->window, app->options.json);
    app->window = {};
    app->next_report_frame += rate;
  }

  spa_data->chunk->offset = 0;
  spa_data->chunk->size = frames * channels * sizeof(float);
  spa_data->chunk->stride = static_cast<int32_t>(channels * sizeof(float));

  pw_stream_queue_buffer(app->stream, pw_buffer);

  if (app->options.duration_seconds != 0 &&
      app->frame >=
          static_cast<uint64_t>(app->options.duration_seconds) * rate) {
    pw_main_loop_quit(app->main_loop);
  }
}

void on_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto *app = static_cast<App *>(data);
  if (param == nullptr || id != SPA_PARAM_Format)
    return;

  uint32_t media_type = 0;
  uint32_t media_subtype = 0;
  if (spa_format_parse(param, &media_type, &media_subtype) < 0)
    return;
  if (media_type != SPA_MEDIA_TYPE_audio ||
      media_subtype != SPA_MEDIA_SUBTYPE_raw)
    return;

  spa_format_audio_raw_parse(param, &app->format);
}

const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_param_changed,
    .process = on_process,
};

void on_registry_global(void *data, uint32_t id, uint32_t, const char *type,
                        uint32_t, const spa_dict *props) {
  auto *app = static_cast<App *>(data);
  if (app == nullptr || app->node_identity_printed || type == nullptr ||
      std::strcmp(type, PW_TYPE_INTERFACE_Node) != 0 || props == nullptr)
    return;

  const auto stream_node_id =
      app->stream == nullptr ? PW_ID_ANY : pw_stream_get_node_id(app->stream);
  if (stream_node_id != PW_ID_ANY && stream_node_id != id)
    return;

  const auto *name = spa_dict_lookup(props, PW_KEY_NODE_NAME);
  if ((stream_node_id == PW_ID_ANY || app->stream == nullptr) &&
      (name == nullptr || app->options.name != name))
    return;

  const auto *serial = spa_dict_lookup(props, PW_KEY_OBJECT_SERIAL);
  const auto *target = spa_dict_lookup(props, PW_KEY_TARGET_OBJECT);
  if (app->options.json) {
    std::cout << "{\"type\":\"node\",\"app\":\"signal\",\"object_id\":" << id
              << ",\"object_serial\":\""
              << json_escape(serial == nullptr ? "" : serial) << "\",\"name\":\""
              << json_escape(name == nullptr ? "" : name)
              << "\",\"target_object\":\""
              << json_escape(target == nullptr ? "" : target)
              << "\",\"source\":\"" << mode_name(app->options.mode)
              << "\",\"value\":" << app->options.value
              << ",\"high_value\":" << app->options.high_value
              << ",\"frequency\":" << app->options.frequency << "}"
              << std::endl;
    app->node_identity_printed = true;
    return;
  }

  std::cout << "signal node object.id=" << id
            << " object.serial=" << (serial == nullptr ? "" : serial)
            << " name=" << (name == nullptr ? "" : name)
            << " target.object=" << (target == nullptr ? "" : target)
            << " source=" << mode_name(app->options.mode)
            << " value=" << app->options.value
            << " high.value=" << app->options.high_value
            << " frequency=" << app->options.frequency
            << std::endl;
  app->node_identity_printed = true;
}

const pw_registry_events registry_events = {
    .version = PW_VERSION_REGISTRY_EVENTS,
    .global = on_registry_global,
};

void on_signal(int) {
  if (g_app != nullptr && g_app->main_loop != nullptr)
    pw_main_loop_quit(g_app->main_loop);
}

} // namespace

int main(int argc, char **argv) {
  App app;
  if (!parse_args(argc, argv, app.options)) {
    print_usage(argv[0]);
    return 2;
  }
  app.format.format = SPA_AUDIO_FORMAT_F32;
  app.format.rate = app.options.rate;
  app.format.channels = app.options.channels;
  g_app = &app;

  std::signal(SIGINT, on_signal);
  std::signal(SIGTERM, on_signal);

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr)
    return 1;
  app.context = pw_context_new(pw_main_loop_get_loop(app.main_loop), nullptr, 0);
  if (app.context == nullptr)
    return 1;
  app.core = pw_context_connect(app.context, nullptr, 0);
  if (app.core == nullptr)
    return 1;
  app.registry = pw_core_get_registry(app.core, PW_VERSION_REGISTRY, 0);
  if (app.registry == nullptr)
    return 1;
  pw_registry_add_listener(app.registry, &app.registry_listener,
                           &registry_events, &app);
  if (app.options.mode == Mode::BeatAlternating) {
    app.sync_client =
        std::make_shared<sesync::SyncClient>(app.core,
                                             pw_main_loop_get_loop(app.main_loop));
    app.sync_client->start();
  }

  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "Test", PW_KEY_MEDIA_CLASS, "Stream/Output/Audio",
      PW_KEY_NODE_NAME, app.options.name.c_str(), PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy test signal", nullptr);
  if (app.options.target)
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT,
                      app.options.target->c_str());

  app.stream =
      pw_stream_new(app.core, app.options.name.c_str(), properties);
  if (app.stream == nullptr)
    return 1;
  pw_stream_add_listener(app.stream, &app.stream_listener, &stream_events,
                         &app);

  std::array<uint8_t, 1024> pod_buffer{};
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, pod_buffer.data(), pod_buffer.size());
  spa_audio_info_raw audio_info{};
  audio_info.format = SPA_AUDIO_FORMAT_F32;
  audio_info.rate = app.options.rate;
  audio_info.channels = app.options.channels;
  const spa_pod *params[1] = {
      spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &audio_info)};

  auto flags = static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                            PW_STREAM_FLAG_RT_PROCESS);
  if (app.options.target)
    flags = static_cast<pw_stream_flags>(flags | PW_STREAM_FLAG_AUTOCONNECT);

  if (pw_stream_connect(app.stream, PW_DIRECTION_OUTPUT, PW_ID_ANY, flags,
                        params, 1) < 0)
    return 1;

  pw_main_loop_run(app.main_loop);
  print_stats("total", app.total, app.options.json);

  if (app.sync_client)
    app.sync_client->stop();
  pw_stream_destroy(app.stream);
  pw_proxy_destroy(reinterpret_cast<pw_proxy *>(app.registry));
  pw_core_disconnect(app.core);
  pw_context_destroy(app.context);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
