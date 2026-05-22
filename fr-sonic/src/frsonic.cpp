#include "../include/frsonic.h"

#include "core/Core.h"
#include "monitoring/Monitor.h"
#include "midi/MidiSyncSender.h"
#include "midi/Processor.h"
#include "lv2/frlv2.h"
#include "sync/SyncClient.h"
#include "sync/SyncMaster.h"
#include "wireplumber/props/props_handling.h"
#include "wireplumber/params/params_handling.h"
#include "wireplumber/modules/module_factory.h"

#include <future>
#include <memory>

static std::shared_ptr<Core>                g_core    = nullptr;
static std::shared_ptr<monitoring::Monitor> g_monitor = nullptr;
static std::shared_ptr<midi::Processor>     g_midi    = nullptr;
static std::shared_ptr<sesync::SyncMaster>  g_sync_master = nullptr;
static std::shared_ptr<sesync::SyncClient>  g_sync_client = nullptr;
static std::shared_ptr<midi::MidiSyncSender> g_midi_sync_sender = nullptr;

static peak_callback_t          g_peak_cb           = nullptr;
static uint64_t                 g_peak_interval_ms  = 0;
static midi_cc_update_callback_t g_midi_cc_cb       = nullptr;

/* ── helpers: convert public-API enums to internal ones ─────────────────── */

static inline controllers::ChannelType to_ct(ChannelType ct) {
    return static_cast<controllers::ChannelType>(ct);
}
static inline controllers::DialMode to_dm(DialMode dm) {
    return static_cast<controllers::DialMode>(dm);
}

/* ── lifecycle ───────────────────────────────────────────────────────────── */

void frsonic_init(
    node_added_callback_t             node_added,
    props_changed_callback_t          props_changed,
    props_enum_failed_callback_t      props_enum_failed,
    prop_info_added_callback_t        prop_info_added,
    object_deleted_callback_t         object_deleted,
    metadata_added_callback_t         metadata_added,
    metadata_entry_updated_callback_t metadata_entry_updated,
    metadata_entry_deleted_callback_t metadata_entry_deleted,
    peak_callback_t                   peak,
    uint64_t                          peak_update_interval_ms,
    midi_cc_update_callback_t         midi_cc_update) {
    g_core = std::make_shared<Core>(
        node_added, props_changed, props_enum_failed, prop_info_added,
        object_deleted, metadata_added, metadata_entry_updated,
        metadata_entry_deleted);
    g_peak_cb          = peak;
    g_peak_interval_ms = peak_update_interval_ms;
    g_midi_cc_cb       = midi_cc_update;
}

struct StartData {
    std::promise<void> done;
};

void frsonic_start() {
    g_core->start();  // blocks until the GLib main loop is running

    auto *data = new StartData{};
    auto future = data->done.get_future();

    g_main_context_invoke(
        g_core->wireplumber_context(),
        [](gpointer user_data) -> gboolean {
            auto *d           = static_cast<StartData *>(user_data);
            auto *loop        = g_core->pipewire_loop();
            auto *pw_core_ptr = g_core->pipewire_core();

            g_sync_master = std::make_shared<sesync::SyncMaster>(
                pw_core_ptr, loop, sesync::SyncMasterConfig{});
            g_sync_master->start();

            g_sync_client = std::make_shared<sesync::SyncClient>(
                pw_core_ptr, loop);
            g_sync_client->start();

            g_midi_sync_sender =
                std::make_shared<midi::MidiSyncSender>(loop, *g_sync_client);
            g_midi_sync_sender->start();

            g_monitor = std::make_shared<monitoring::Monitor>(
                loop, g_peak_cb, g_peak_interval_ms);
            g_monitor->start();

            g_midi = std::make_shared<midi::Processor>(loop, pw_core_ptr,
                [](controllers::ChannelType ct, uint64_t channel_id,
                   uint64_t object_id, const char *parameter_name,
                   float normalized_value, float normalized_known_value,
                   bool catching_up) {
                    if (g_midi_cc_cb)
                        g_midi_cc_cb(static_cast<ChannelType>(ct), channel_id,
                                     object_id, parameter_name, normalized_value,
                                     normalized_known_value, catching_up);
                });
            g_midi->start();

            d->done.set_value();
            delete d;
            return G_SOURCE_REMOVE;
        },
        data);

    future.wait();
}

void frsonic_stop() {
    if (g_midi_sync_sender) { g_midi_sync_sender->stop(); g_midi_sync_sender = nullptr; }
    if (g_sync_client) { g_sync_client->stop(); g_sync_client = nullptr; }
    if (g_sync_master) { g_sync_master->stop(); g_sync_master = nullptr; }
    if (g_monitor) { g_monitor->stop(); g_monitor = nullptr; }
    if (g_midi)    { g_midi->stop();    g_midi    = nullptr; }
    if (g_core)    { g_core->stop();    g_core    = nullptr; }
}

/* ── wireplumber / object model ──────────────────────────────────────────── */

void frsonic_set_volumes(uint64_t object_id, const double *volumes, size_t count) {
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

void frsonic_create_link_by_port_ids(uint64_t out_port_id,
                                     uint64_t in_port_id, bool linger) {
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

size_t frsonic_create_midi_mix_port(
    const char *pmx_purpose, const char *pmx_tag,
    layer_select_callback_t layer_cb,
    channel_select_callback_t channel_cb,
    dial_mode_callback_t dial_mode_cb,
    filter_section_callback_t filter_section_cb) {
    return *g_midi->create_midi_mix_port(pmx_purpose, pmx_tag,
        layer_cb,
        [channel_cb](controllers::ChannelType ct, size_t id) {
            channel_cb(static_cast<ChannelType>(ct), id); },
        [dial_mode_cb](controllers::ChannelType ct, size_t id, controllers::DialMode dm) {
            dial_mode_cb(static_cast<ChannelType>(ct), id, static_cast<DialMode>(dm)); },
        [filter_section_cb](controllers::ChannelType ct, size_t id, size_t sec) {
            filter_section_cb(static_cast<ChannelType>(ct), id, sec); });
}

size_t frsonic_create_mm1_port(
    const char *pmx_purpose, const char *pmx_tag,
    layer_select_callback_t layer_cb,
    channel_select_callback_t channel_cb,
    dial_mode_callback_t dial_mode_cb,
    filter_section_callback_t filter_section_cb,
    pages_right_callback_t pages_right_cb,
    pages_left_callback_t pages_left_cb) {
    return *g_midi->create_mm_1_port(pmx_purpose, pmx_tag,
        layer_cb,
        [channel_cb](controllers::ChannelType ct, size_t id) {
            channel_cb(static_cast<ChannelType>(ct), id); },
        [dial_mode_cb](controllers::ChannelType ct, size_t id, controllers::DialMode dm) {
            dial_mode_cb(static_cast<ChannelType>(ct), id, static_cast<DialMode>(dm)); },
        [filter_section_cb](controllers::ChannelType ct, size_t id, size_t sec) {
            filter_section_cb(static_cast<ChannelType>(ct), id, sec); },
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

void frsonic_set_channel_filter_node(ChannelType channel_type, size_t channel_id,
                                     uint64_t object_id) {
    g_midi->set_channel_filter_node(to_ct(channel_type), channel_id, object_id);
}

void frsonic_set_channel_send_node(ChannelType channel_type, size_t channel_id,
                                   size_t send_id, uint64_t object_id) {
    g_midi->set_channel_send_node(to_ct(channel_type), channel_id,
                                  send_id, object_id);
}

void frsonic_clear_filter_parameters(ChannelType channel_type, size_t channel_id) {
    g_midi->clear_filter_parameters(to_ct(channel_type), channel_id);
}

void frsonic_add_filter_parameter(ChannelType channel_type, size_t channel_id,
                                  size_t plugin_id, char *name, float min,
                                  float max) {
    g_midi->add_filter_parameter(to_ct(channel_type), channel_id,
                                 plugin_id, name, min, max);
}

/* ── lv2 ─────────────────────────────────────────────────────────────────── */

void        frsonic_lv2_init()    { init(); }
void        frsonic_lv2_destroy() { destroy(); }
const char *frsonic_lv2_plugin_descriptions_json() {
    return plugin_descriptions_json();
}
const char *frsonic_lv2_plugin_classes_json() {
    return plugin_classes_json();
}
