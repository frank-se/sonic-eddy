#include "../include/frsonic.h"

#include "core/Core.h"
#include "looper/Looper.h"
#include "lv2/frlv2.h"
#include "midi/MidiManipulator.h"
#include "midi/MidiSyncSender.h"
#include "midi/Processor.h"
#include "monitoring/Monitor.h"
#include "silence/SilenceProducer.h"
#include "sync/SyncClient.h"
#include "sync/SyncMaster.h"
#include "wireplumber/modules/module_factory.h"
#include "wireplumber/params/params_handling.h"
#include "wireplumber/props/props_handling.h"

#include <algorithm>
#include <iostream>
#include <iterator>
#include <memory>
#include <pipewire/pipewire.h>
#include <pipewire/thread-loop.h>
#include <string>
#include <utility>
#include <vector>

static std::shared_ptr<Core> g_core = nullptr;
static std::shared_ptr<monitoring::Monitor> g_monitor = nullptr;
static std::shared_ptr<midi::Processor> g_midi = nullptr;
static std::shared_ptr<sesync::SyncMaster> g_sync_master = nullptr;
static std::shared_ptr<sesync::SyncClient> g_sync_client = nullptr;
static std::shared_ptr<midi::MidiSyncSender> g_midi_sync_sender = nullptr;
static std::vector<std::shared_ptr<looper::Looper>> g_loopers;
static std::vector<size_t> g_free_looper_handles;
static std::vector<std::shared_ptr<midi::MidiManipulator>> g_midi_manipulators;

class OwnedPipewireRuntime {
public:
  bool start() {
    if (_thread_loop != nullptr)
      return true;

    _thread_loop = pw_thread_loop_new("fr-sonic-owned-nodes", nullptr);
    if (_thread_loop == nullptr) {
      std::cerr << "Failed to create owned PipeWire thread loop" << std::endl;
      return false;
    }

    _loop = pw_thread_loop_get_loop(_thread_loop);
    _context = pw_context_new(_loop, nullptr, 0);
    if (_context == nullptr) {
      std::cerr << "Failed to create owned PipeWire context" << std::endl;
      stop();
      return false;
    }

    _core = pw_context_connect(_context, nullptr, 0);
    if (_core == nullptr) {
      std::cerr << "Failed to connect owned PipeWire core" << std::endl;
      stop();
      return false;
    }

    if (pw_thread_loop_start(_thread_loop) < 0) {
      std::cerr << "Failed to start owned PipeWire thread loop" << std::endl;
      stop();
      return false;
    }

    return true;
  }

  void stop() {
    if (_thread_loop != nullptr)
      pw_thread_loop_stop(_thread_loop);

    if (_core != nullptr) {
      pw_core_disconnect(_core);
      _core = nullptr;
    }

    if (_context != nullptr) {
      pw_context_destroy(_context);
      _context = nullptr;
    }

    if (_thread_loop != nullptr) {
      pw_thread_loop_destroy(_thread_loop);
      _thread_loop = nullptr;
    }

    _loop = nullptr;
  }

  [[nodiscard]] pw_loop *loop() const { return _loop; }
  [[nodiscard]] pw_core *core() const { return _core; }
  [[nodiscard]] pw_context *context() const { return _context; }

private:
  pw_thread_loop *_thread_loop = nullptr;
  pw_loop *_loop = nullptr;
  pw_context *_context = nullptr;
  pw_core *_core = nullptr;
};

static std::unique_ptr<OwnedPipewireRuntime> g_owned_pipewire = nullptr;

static peak_callback_t g_peak_cb = nullptr;
static uint64_t g_peak_interval_ms = 0;
static midi_cc_update_callback_t g_midi_cc_cb = nullptr;

/* ── helpers: convert public-API enums to internal ones ─────────────────── */

static inline controllers::ChannelType to_ct(ChannelType ct) {
  return static_cast<controllers::ChannelType>(ct);
}
static inline controllers::DialMode to_dm(DialMode dm) {
  return static_cast<controllers::DialMode>(dm);
}

/* ── lifecycle ───────────────────────────────────────────────────────────── */

void frsonic_init(node_added_callback_t node_added,
                  props_changed_callback_t props_changed,
                  props_enum_failed_callback_t props_enum_failed,
                  prop_info_added_callback_t prop_info_added,
                  object_deleted_callback_t object_deleted,
                  metadata_added_callback_t metadata_added,
                  metadata_entry_updated_callback_t metadata_entry_updated,
                  metadata_entry_deleted_callback_t metadata_entry_deleted,
                  peak_callback_t peak, uint64_t peak_update_interval_ms,
                  midi_cc_update_callback_t midi_cc_update) {
  g_core =
      std::make_shared<Core>(node_added, props_changed, props_enum_failed,
                             prop_info_added, object_deleted, metadata_added,
                             metadata_entry_updated, metadata_entry_deleted);
  g_peak_cb = peak;
  g_peak_interval_ms = peak_update_interval_ms;
  g_midi_cc_cb = midi_cc_update;
}

struct OwnedInvokeData {
  void (*callback)();
};

static void invoke_owned_pipewire(void (*callback)()) {
  OwnedInvokeData data{.callback = callback};
  pw_loop_invoke(
      g_owned_pipewire->loop(),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        static_cast<OwnedInvokeData *>(user_data)->callback();
        return 0;
      },
      0, nullptr, 0, true, &data);
}

template <typename Callback>
static void invoke_owned_pipewire_sync(Callback &callback) {
  pw_loop_invoke(
      g_owned_pipewire->loop(),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        (*static_cast<Callback *>(user_data))();
        return 0;
      },
      0, nullptr, 0, true, &callback);
}

static std::string optional_c_string(const char *value,
                                     const char *fallback = "") {
  return value == nullptr ? std::string(fallback) : std::string(value);
}

static std::optional<std::string> optional_target_object(const char *value) {
  if (value == nullptr || value[0] == '\0')
    return std::nullopt;
  return std::string(value);
}

static size_t store_looper(std::shared_ptr<looper::Looper> looper) {
  if (!g_free_looper_handles.empty()) {
    const auto handle = g_free_looper_handles.back();
    g_free_looper_handles.pop_back();
    if (handle < g_loopers.size()) {
      g_loopers[handle] = std::move(looper);
      return handle;
    }
  }

  g_loopers.push_back(std::move(looper));
  return g_loopers.size() - 1;
}

static size_t
store_midi_manipulator(std::shared_ptr<midi::MidiManipulator> manipulator) {
  g_midi_manipulators.push_back(std::move(manipulator));
  return g_midi_manipulators.size() - 1;
}

void frsonic_start() {
  g_core->start(); // blocks until the GLib main loop is running

  g_owned_pipewire = std::make_unique<OwnedPipewireRuntime>();
  if (!g_owned_pipewire->start())
    return;

  invoke_owned_pipewire([] {
    auto *loop = g_owned_pipewire->loop();
    auto *pw_core_ptr = g_owned_pipewire->core();

    g_sync_master = std::make_shared<sesync::SyncMaster>(
        pw_core_ptr, loop, sesync::SyncMasterConfig{});
    g_sync_master->start();

    g_sync_client = std::make_shared<sesync::SyncClient>(pw_core_ptr, loop);
    g_sync_client->start();

    g_midi_sync_sender =
        std::make_shared<midi::MidiSyncSender>(loop, *g_sync_client);
    g_midi_sync_sender->start();

    g_monitor = std::make_shared<monitoring::Monitor>(loop, g_peak_cb,
                                                      g_peak_interval_ms);
    g_monitor->start();

    g_midi = std::make_shared<midi::Processor>(
        loop, pw_core_ptr,
        [](controllers::ChannelType ct, uint64_t channel_id, uint64_t object_id,
           const char *parameter_name, float normalized_value,
           float normalized_known_value, bool catching_up) {
          if (g_midi_cc_cb)
            g_midi_cc_cb(static_cast<ChannelType>(ct), channel_id, object_id,
                         parameter_name, normalized_value,
                         normalized_known_value, catching_up);
        });
    g_midi->start();
  });
}

void frsonic_stop() {
  if (g_owned_pipewire) {
    auto callback = [] {
      for (auto &looper : g_loopers) {
        if (looper)
          looper->stop();
      }
      g_loopers.clear();
      g_free_looper_handles.clear();
      for (auto &manipulator : g_midi_manipulators) {
        if (manipulator)
          manipulator->stop();
      }
      g_midi_manipulators.clear();
      if (g_midi_sync_sender) {
        g_midi_sync_sender->stop();
        g_midi_sync_sender = nullptr;
      }
      if (g_sync_client) {
        g_sync_client->stop();
        g_sync_client = nullptr;
      }
      if (g_sync_master) {
        g_sync_master->stop();
        g_sync_master = nullptr;
      }
      if (g_monitor) {
        g_monitor->stop();
        g_monitor = nullptr;
      }
      if (g_midi) {
        g_midi->stop();
        g_midi = nullptr;
      }
    };
    invoke_owned_pipewire_sync(callback);
    g_owned_pipewire->stop();
    g_owned_pipewire = nullptr;
  }

  if (g_core) {
    g_core->stop();
    g_core = nullptr;
  }
}

bool frsonic_create_looper(const frsonic_looper_config *config,
                           size_t *out_handle) {
  if (config == nullptr || out_handle == nullptr || !g_owned_pipewire)
    return false;

  bool result = false;
  auto callback = [&] {
    looper::LooperConfig looper_config{
        .name = optional_c_string(config->name, "se.looper"),
        .tag = optional_c_string(config->tag),
        .description =
            optional_c_string(config->description, "Sonic Eddy looper"),
        .capture_target_object =
            optional_target_object(config->capture_target_object),
        .playback_target_object =
            optional_target_object(config->playback_target_object),
        .archive_folder_path =
            optional_target_object(config->archive_folder_path),
        .channels = config->channels == 0 ? 2u : config->channels,
        .max_record_seconds =
            config->max_record_seconds == 0 ? 300u : config->max_record_seconds,
        .mix = std::clamp(config->mix, 0.0f, 1.0f),
    };

    auto looper = std::make_shared<looper::Looper>(
        g_owned_pipewire->loop(), std::move(looper_config), g_sync_client);
    if (!looper->start())
      return;

    *out_handle = store_looper(std::move(looper));
    result = true;
  };
  invoke_owned_pipewire_sync(callback);
  return result;
}

void frsonic_destroy_looper(const size_t looper_handle) {
  if (!g_owned_pipewire)
    return;

  auto callback = [looper_handle] {
    if (looper_handle >= g_loopers.size() || !g_loopers[looper_handle])
      return;

    g_loopers[looper_handle]->stop();
    g_loopers[looper_handle].reset();
    g_free_looper_handles.push_back(looper_handle);
  };
  invoke_owned_pipewire_sync(callback);
}

bool frsonic_create_midi_manipulator(
    const frsonic_midi_manipulator_config *config, size_t *out_handle) {
  if (config == nullptr || out_handle == nullptr || !g_owned_pipewire)
    return false;

  bool result = false;
  auto callback = [&] {
    midi::MidiManipulatorConfig manipulator_config{
        .name = optional_c_string(config->name, "se.midi_manipulator"),
        .tag = optional_c_string(config->tag),
        .description = optional_c_string(config->description,
                                         "Sonic Eddy MIDI manipulator"),
    };

    auto manipulator = std::make_shared<midi::MidiManipulator>(
        g_owned_pipewire->loop(), std::move(manipulator_config));
    if (!manipulator->start())
      return;

    *out_handle = store_midi_manipulator(std::move(manipulator));
    result = true;
  };
  invoke_owned_pipewire_sync(callback);
  return result;
}

void frsonic_destroy_midi_manipulator(const size_t manipulator_handle) {
  if (!g_owned_pipewire)
    return;

  auto callback = [manipulator_handle] {
    if (manipulator_handle >= g_midi_manipulators.size()) {
      logging::log<logging::LogLevel::Error>(
          "Ignoring invalid MIDI manipulator destroy handle {} with size {}",
          manipulator_handle, g_midi_manipulators.size());
      return;
    }

    auto slot = std::next(g_midi_manipulators.begin(),
                          static_cast<std::ptrdiff_t>(manipulator_handle));
    if (!*slot)
      return;

    (*slot)->stop();
    slot->reset();
  };
  invoke_owned_pipewire_sync(callback);
}

/* ── wireplumber / object model ──────────────────────────────────────────── */

void frsonic_set_volumes(uint64_t object_id, const double *volumes,
                         size_t count) {
  props::props_handling::set_volumes(object_id, volumes, count, g_core);
}

void frsonic_set_mute(uint64_t object_id, bool mute) {
  props::props_handling::set_mute(object_id, mute, g_core);
}

void frsonic_set_params(uint64_t object_id, char **keys,
                        models::params::ParamType *param_types,
                        uint64_t *values, size_t count) {
  params::params_handling::set_params(object_id, keys, param_types, values,
                                      count, g_core);
}

bool frsonic_load_module(const char *name, const char *config, void **handle) {
  return modules::modules_factory::load_module(name, config, handle, g_core);
}

void frsonic_destroy_module(void *handle) {
  modules::modules_factory::destroy_module(handle);
}

void frsonic_set_metadata_entry(const char *metadata_name, uint64_t subject,
                                const char *key, const char *type,
                                const char *value) {
  g_core->set_metadata_entry(metadata_name, subject, key, type, value);
}

void frsonic_delete_metadata_entry(const char *metadata_name, uint64_t subject,
                                   const char *key) {
  g_core->delete_metadata_entry(metadata_name, subject, key);
}

void frsonic_create_link_by_port_ids(uint64_t out_port_id, uint64_t in_port_id,
                                     bool linger) {
  g_core->create_link_by_port_id(out_port_id, in_port_id, linger);
}

void frsonic_delete_link_by_object_id(uint64_t object_id) {
  g_core->delete_link_object_id(object_id);
}

/* ── monitoring ──────────────────────────────────────────────────────────── */

void frsonic_start_monitor_node(uint64_t object_serial) {
  g_monitor->start_monitor_node(object_serial);
}

void frsonic_stop_monitor_node(uint64_t object_serial) {
  g_monitor->stop_monitor_node(object_serial);
}

/* ── midi ────────────────────────────────────────────────────────────────── */

size_t
frsonic_create_midi_mix_port(const char *pmx_purpose, const char *pmx_tag,
                             layer_select_callback_t layer_cb,
                             channel_select_callback_t channel_cb,
                             dial_mode_callback_t dial_mode_cb,
                             filter_section_callback_t filter_section_cb) {
  return *g_midi->create_midi_mix_port(
      pmx_purpose, pmx_tag, layer_cb,
      [channel_cb](controllers::ChannelType ct, size_t id) {
        channel_cb(static_cast<ChannelType>(ct), id);
      },
      [dial_mode_cb](controllers::ChannelType ct, size_t id,
                     controllers::DialMode dm) {
        dial_mode_cb(static_cast<ChannelType>(ct), id,
                     static_cast<DialMode>(dm));
      },
      [filter_section_cb](controllers::ChannelType ct, size_t id, size_t sec) {
        filter_section_cb(static_cast<ChannelType>(ct), id, sec);
      });
}

size_t frsonic_create_mm1_port(const char *pmx_purpose, const char *pmx_tag,
                               layer_select_callback_t layer_cb,
                               channel_select_callback_t channel_cb,
                               dial_mode_callback_t dial_mode_cb,
                               filter_section_callback_t filter_section_cb,
                               pages_right_callback_t pages_right_cb,
                               pages_left_callback_t pages_left_cb) {
  return *g_midi->create_mm_1_port(
      pmx_purpose, pmx_tag, layer_cb,
      [channel_cb](controllers::ChannelType ct, size_t id) {
        channel_cb(static_cast<ChannelType>(ct), id);
      },
      [dial_mode_cb](controllers::ChannelType ct, size_t id,
                     controllers::DialMode dm) {
        dial_mode_cb(static_cast<ChannelType>(ct), id,
                     static_cast<DialMode>(dm));
      },
      [filter_section_cb](controllers::ChannelType ct, size_t id, size_t sec) {
        filter_section_cb(static_cast<ChannelType>(ct), id, sec);
      },
      pages_right_cb, pages_left_cb);
}

size_t frsonic_create_fader_fox_pc4_port(const char *pmx_purpose,
                                         const char *pmx_tag) {
  return *g_midi->create_fader_fox_pc4_port(pmx_purpose, pmx_tag);
}

void frsonic_set_selected_plugin_page(size_t plugin_id, size_t page_number) {
  g_midi->set_selected_plugin_page(plugin_id, page_number);
}

void frsonic_set_selected_channel(ChannelType channel_type, size_t channel_id) {
  g_midi->set_selected_channel(to_ct(channel_type), channel_id);
}

void frsonic_clear_selected_channel() { g_midi->clear_selected_channel(); }

void frsonic_set_selected_layer(size_t layer_id) {
  g_midi->set_selected_layer(layer_id);
}

void frsonic_set_channel_node(ChannelType channel_type, size_t channel_id,
                              uint64_t object_id) {
  g_midi->set_channel_node(to_ct(channel_type), channel_id, object_id);
}

void frsonic_set_master_channel_node(size_t layer_id, uint64_t object_id) {
  g_midi->set_master_channel_node(layer_id, object_id);
}

void frsonic_set_channel_filter_node(ChannelType channel_type,
                                     size_t channel_id, uint64_t object_id) {
  g_midi->set_channel_filter_node(to_ct(channel_type), channel_id, object_id);
}

void frsonic_set_channel_send_node(ChannelType channel_type, size_t channel_id,
                                   size_t send_id, uint64_t object_id) {
  g_midi->set_channel_send_node(to_ct(channel_type), channel_id, send_id,
                                object_id);
}

void frsonic_clear_filter_parameters(ChannelType channel_type,
                                     size_t channel_id) {
  g_midi->clear_filter_parameters(to_ct(channel_type), channel_id);
}

void frsonic_add_filter_parameter(ChannelType channel_type, size_t channel_id,
                                  size_t plugin_id, char *name, float min,
                                  float max) {
  g_midi->add_filter_parameter(to_ct(channel_type), channel_id, plugin_id, name,
                               min, max);
}

/* ── lv2 ─────────────────────────────────────────────────────────────────── */

void frsonic_lv2_init() { init(); }
void frsonic_lv2_destroy() { destroy(); }
const char *frsonic_lv2_plugin_descriptions_json() {
  return plugin_descriptions_json();
}
const char *frsonic_lv2_plugin_classes_json() { return plugin_classes_json(); }

/* ── silence producers ───────────────────────────────────────────────────── */

void *frsonic_create_silence_producer(uint64_t target_serial) {
  auto *producer =
      new silence::SilenceProducer(target_serial, g_owned_pipewire->loop());

  struct InvokeData {
    silence::SilenceProducer *producer;
  };
  InvokeData data{producer};

  pw_loop_invoke(
      g_owned_pipewire->loop(),
      [](spa_loop *, bool, uint32_t, const void *, size_t,
         void *user_data) -> int {
        static_cast<InvokeData *>(user_data)->producer->setup();
        return 0;
      },
      0, nullptr, 0, true, &data);

  return producer;
}

void frsonic_destroy_silence_producer(void *handle) {
  if (!handle)
    return;

  struct InvokeData {
    silence::SilenceProducer *producer;
  };
  InvokeData data{static_cast<silence::SilenceProducer *>(handle)};

  pw_loop_invoke(
      g_owned_pipewire->loop(),
      [](spa_loop *, bool, uint32_t, const void *, size_t,
         void *user_data) -> int {
        delete static_cast<InvokeData *>(user_data)->producer;
        return 0;
      },
      0, nullptr, 0, true, &data);
}
