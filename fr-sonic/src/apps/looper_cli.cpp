#include "../include/frsonic.h"

#include "wireplumber/models/objects/wireplumber_object.h"

#include <algorithm>
#include <chrono>
#include <cstdlib>
#include <iostream>
#include <optional>
#include <string>
#include <string_view>
#include <thread>

namespace {

struct Options {
  std::string name = "se.cli_looper";
  std::string tag = "cli-looper";
  std::optional<std::string> capture_target;
  std::optional<std::string> playback_target;
  std::optional<std::string> archive_folder_path;
  uint32_t channels = 2;
  uint32_t max_record_seconds = 300;
  uint32_t duration_seconds = 0;
  float mix = 0.0f;
};

const Options *g_options = nullptr;

void on_node_added(const wireplumber_object *node) {
  if (g_options == nullptr || node == nullptr ||
      node->type != wireplumber_object_type::node)
    return;

  const auto tag = node->pmx_tag == nullptr ? std::string_view{} : node->pmx_tag;
  if (tag != g_options->tag)
    return;

  const auto purpose =
      node->pmx_purpose == nullptr ? std::string_view{} : node->pmx_purpose;
  if (!purpose.starts_with("looper-"))
    return;

  std::cout << "looper node object.id=" << node->object_id
            << " object.serial=" << node->object_serial
            << " name=" << (node->node_name == nullptr ? "" : node->node_name)
            << " purpose=" << purpose
            << " tag=" << (node->pmx_tag == nullptr ? "" : node->pmx_tag)
            << " media.class="
            << (node->media_class == nullptr ? "" : node->media_class)
            << " target.object="
            << (node->target_object == nullptr ? "" : node->target_object)
            << std::endl;
}

void on_props_changed(const Props *, const ParamUpdate *) {}
void on_props_enum_failed(uint64_t) {}
void on_prop_info_added(const char *) {}
void on_object_deleted(uint64_t, wireplumber_object_type) {}
void on_metadata_added(const char *) {}
void on_metadata_entry_updated(const char *, uint64_t, const char *,
                               const char *, const char *) {}
void on_metadata_entry_deleted(const char *, uint64_t, const char *) {}
void on_peak(uint64_t, float, float, float, float) {}
void on_midi_cc_update(ChannelType, uint64_t, uint64_t, const char *, float,
                       float, bool) {}

void print_usage(const char *argv0) {
  std::cerr
      << "Usage: " << argv0
      << " [-n name] [-t tag] [-c capture-target] [-p playback-target]\n"
      << "       [--archive-folder path] [--channels n]\n"
      << "       [--max-record-seconds n] [--mix value] [-d seconds]\n";
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

    if (arg == "-n" || arg == "--name") {
      if (const auto *value = require_value(arg.c_str()))
        options.name = value;
      else
        return false;
    } else if (arg == "-t" || arg == "--tag") {
      if (const auto *value = require_value(arg.c_str()))
        options.tag = value;
      else
        return false;
    } else if (arg == "-c" || arg == "--capture-target") {
      if (const auto *value = require_value(arg.c_str()))
        options.capture_target = value;
      else
        return false;
    } else if (arg == "-p" || arg == "--playback-target") {
      if (const auto *value = require_value(arg.c_str()))
        options.playback_target = value;
      else
        return false;
    } else if (arg == "--archive-folder") {
      if (const auto *value = require_value(arg.c_str()))
        options.archive_folder_path = value;
      else
        return false;
    } else if (arg == "--channels") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_uint(value, options.channels))
        return false;
    } else if (arg == "--max-record-seconds") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_uint(value, options.max_record_seconds))
        return false;
    } else if (arg == "--mix") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_float(value, options.mix))
        return false;
      options.mix = std::clamp(options.mix, 0.0f, 1.0f);
    } else if (arg == "-d" || arg == "--duration") {
      if (const auto *value = require_value(arg.c_str());
          value == nullptr || !parse_uint(value, options.duration_seconds))
        return false;
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

} // namespace

int main(int argc, char **argv) {
  Options options;
  if (!parse_args(argc, argv, options)) {
    print_usage(argv[0]);
    return 2;
  }
  g_options = &options;

  frsonic_init(on_node_added, on_props_changed, on_props_enum_failed,
               on_prop_info_added, on_object_deleted, on_metadata_added,
               on_metadata_entry_updated, on_metadata_entry_deleted, on_peak,
               100, on_midi_cc_update);
  frsonic_start();

  const frsonic_looper_config config{
      .name = options.name.c_str(),
      .tag = options.tag.c_str(),
      .description = "Sonic Eddy CLI looper",
      .capture_target_object =
          options.capture_target ? options.capture_target->c_str() : nullptr,
      .playback_target_object =
          options.playback_target ? options.playback_target->c_str() : nullptr,
      .archive_folder_path = options.archive_folder_path
                                  ? options.archive_folder_path->c_str()
                                  : nullptr,
      .channels = options.channels,
      .max_record_seconds = options.max_record_seconds,
      .mix = options.mix,
  };

  size_t handle = 0;
  if (!frsonic_create_looper(&config, &handle)) {
    std::cerr << "Failed to create looper\n";
    frsonic_stop();
    return 1;
  }

  std::cout << "created looper name=" << options.name << " tag=" << options.tag
            << " handle=" << handle << " mix=" << options.mix << std::endl;

  if (options.duration_seconds == 0) {
    std::cout << "press Enter to stop\n";
    std::cin.get();
  } else {
    std::this_thread::sleep_for(
        std::chrono::seconds(options.duration_seconds));
  }

  frsonic_destroy_looper(handle);
  frsonic_stop();
  return 0;
}
