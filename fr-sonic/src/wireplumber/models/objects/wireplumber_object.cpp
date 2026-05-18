#include "wireplumber_object.h"

#include <pipewire/keys.h>
#include <wp/wp.h>

#include "spa_helpers/mapping_helper.h"

models::objects::wireplumber_object models::objects::create_node_from_object(
  WpPipewireObject *object) {
  wireplumber_object node = {
    .object_path = read_string(object, PW_KEY_OBJECT_PATH),
    .application_name = read_string(object, PW_KEY_APP_NAME),
    .api_alsa_path = read_string(object, "api.alsa.path"),
    .audio_channel = read_string(object, PW_KEY_AUDIO_CHANNEL),
    .audio_format = read_string(object, PW_KEY_AUDIO_FORMAT),
    .audio_allowed_rates = read_string(object, PW_KEY_AUDIO_ALLOWED_RATES),
    .audio_position = read_string(object, "audio.position"),
    .channel_mix_upmix_method = read_string(object, "channelmix.upmix-method"),
    .clock_name = read_string(object, "clock.name"),
    .debug_wav_path = read_string(object, "debug.wav-path"),
    .device_name = read_string(object, PW_KEY_DEVICE_NAME),
    .device_nick = read_string(object, PW_KEY_DEVICE_NICK),
    .device_description = read_string(object, PW_KEY_DEVICE_DESCRIPTION),
    .device_serial = read_string(object, PW_KEY_DEVICE_SERIAL),
    .device_vendor_id = read_string(object, PW_KEY_DEVICE_VENDOR_ID),
    .device_vendor_name = read_string(object, PW_KEY_DEVICE_VENDOR_NAME),
    .device_product_id = read_string(object, PW_KEY_DEVICE_PRODUCT_ID),
    .device_product_name = read_string(object, PW_KEY_DEVICE_PRODUCT_NAME),
    .device_class = read_string(object, PW_KEY_DEVICE_CLASS),
    .device_form_factor = read_string(object, PW_KEY_DEVICE_FORM_FACTOR),
    .device_icon = read_string(object, PW_KEY_DEVICE_ICON),
    .device_icon_name = read_string(object, PW_KEY_DEVICE_ICON_NAME),
    .device_intended_roles = read_string(object, PW_KEY_DEVICE_INTENDED_ROLES),
    .device_profile_set = read_string(object, "device.profile-set"),
    .device_profile = read_string(object, "device.profile"),
    .device_subsystem = read_string(object, PW_KEY_DEVICE_SUBSYSTEM),
    .dither_method = read_string(object, "dither.method"),
    .format_dsp = read_string(object, PW_KEY_FORMAT_DSP),
    .link_group = read_string(object, "link.group"),
    .media_name = read_string(object, PW_KEY_MEDIA_NAME),
    .media_title = read_string(object, PW_KEY_MEDIA_TITLE),
    .media_artist = read_string(object, PW_KEY_MEDIA_ARTIST),
    .media_copyright = read_string(object, PW_KEY_MEDIA_COPYRIGHT),
    .media_software = read_string(object, PW_KEY_MEDIA_SOFTWARE),
    .media_language = read_string(object, PW_KEY_MEDIA_LANGUAGE),
    .media_filename = read_string(object, PW_KEY_MEDIA_FILENAME),
    .media_icon = read_string(object, PW_KEY_MEDIA_ICON),
    .media_icon_name = read_string(object, PW_KEY_MEDIA_ICON_NAME),
    .media_comment = read_string(object, PW_KEY_MEDIA_COMMENT),
    .media_date = read_string(object, PW_KEY_MEDIA_DATE),
    .media_format = read_string(object, PW_KEY_MEDIA_FORMAT),
    .media_type = read_string(object, PW_KEY_MEDIA_TYPE),
    .media_category = read_string(object, PW_KEY_MEDIA_CATEGORY),
    .media_role = read_string(object, PW_KEY_MEDIA_ROLE),
    .media_class = read_string(object, PW_KEY_MEDIA_CLASS),
    .node_name = read_string(object, PW_KEY_NODE_NAME),
    .node_description = read_string(object, PW_KEY_NODE_DESCRIPTION),
    .node_loop_class = read_string(object, PW_KEY_NODE_LOOP_CLASS),
    .node_link_group = read_string(object, PW_KEY_NODE_LINK_GROUP),
    .pipewire_sec_socket = read_string(object, PW_KEY_SEC_SOCKET),
    .pipewire_access = read_string(object, PW_KEY_ACCESS),
    .pipewire_client_access = read_string(object, PW_KEY_CLIENT_ACCESS),
    .pipewire_protocol = read_string(object, PW_KEY_PROTOCOL),
    .pmx_tag = read_string(object, "pmx.tag"),
    .pmx_purpose = read_string(object, "pmx.purpose"),
    .port_name = read_string(object, PW_KEY_PORT_NAME),
    .port_alias = read_string(object, PW_KEY_PORT_ALIAS),
    .port_group = read_string(object, PW_KEY_PORT_GROUP),
    .port_direction = read_string(object, PW_KEY_PORT_DIRECTION),
    .target_object = read_string(object, PW_KEY_TARGET_OBJECT),
  };

  fill_node_from_object(node, object);
  return node;
}

void models::objects::fill_node_from_object(wireplumber_object &node, WpPipewireObject *object) {
  const char *props = nullptr;

  if (WP_IS_PORT(object)) {
    node.type = wireplumber_object_type::port;
  } else if (WP_IS_LINK(object)) {
    node.type = wireplumber_object_type::link;
  } else if (WP_IS_NODE(object)) {
    node.type = wireplumber_object_type::node;
  } else if (WP_IS_DEVICE(object)) {
    node.type = wireplumber_object_type::device;
  } else if (WP_IS_CLIENT(object)) {
    node.type = wireplumber_object_type::client;
  }

  fill_bool(node.alsa_use_acp, object, "alsa.use-acp");
  fill_bool(node.alsa_udev_expose_busy, object, "alsa.udev-expose-busy");
  fill_uint64(node.application_process_id, object, PW_KEY_APP_PROCESS_ID);
  fill_bool(node.api_acp_auto_profile, object, "api.acp.auto-profile");
  fill_bool(node.api_acp_auto_port, object, "api.acp.auto-port");
  fill_uint64(node.api_acp_probe_rate, object, "api.acp.probe-rate");
  fill_uint64(node.api_acp_pro_channels, object, "api.acp.pro-channels");
  fill_bool(node.api_alsa_use_ucm, object, "api.alsa.use-ucm");
  fill_bool(node.api_alsa_soft_mixer, object, "api.alsa.soft-mixer");
  fill_bool(node.api_alsa_disable_mixer_path, object,
            "api.alsa.disable-mixer-path");
  fill_bool(node.api_alsa_ignore_db, object, "api.alsa.ignore-db");
  fill_bool(node.api_alsa_split_enable, object, "api.alsa.split-enable");
  fill_uint64_with_existence(node.api_alsa_period_size,
                             node.has_api_alsa_period_size, object,
                             "api.alsa.period-size");
  fill_uint64_with_existence(node.api_alsa_period_num,
                             node.has_api_alsa_period_num, object,
                             "api.alsa.period-num");
  fill_uint64(node.api_alsa_headroom, object, "api.alsa.headroom");
  fill_uint64(node.api_alsa_start_delay, object, "api.alsa.start-delay");
  fill_bool(node.api_alsa_disable_mmap, object, "api.alsa.disable-mmap");
  fill_bool(node.api_alsa_disable_batch, object, "api.alsa.disable-batch");
  fill_bool(node.api_alsa_use_chmap, object, "api.alsa.use-chmap");
  fill_bool(node.api_alsa_multi_rate, object, "api.alsa.multi-rate");
  fill_bool(node.api_alsa_htimestamp, object, "api.alsa.htimestamp");
  fill_uint64_with_existence(node.api_alsa_htimestamp_max_errors,
                             node.has_api_alsa_htimestamp_max_errors, object,
                             "api.alsa.htimestamp-max-errors");
  fill_bool(node.api_alsa_disable_tsched, object, "api.alsa.disable-tsched");
  fill_bool(node.api_alsa_auto_link, object, "api.alsa.auto-link");
  fill_uint64_with_existence(node.audio_channels, node.has_audio_channels,
                             object, PW_KEY_AUDIO_CHANNELS);
  fill_uint64_with_existence(node.audio_rate, node.has_audio_rate, object,
                             PW_KEY_AUDIO_RATE);
  fill_bool(node.channel_mix_disable, object, "channelmix.disable");
  fill_double(node.channel_mix_min_volume, object, "channelmix.min-volume");
  fill_double(node.channel_mix_max_volume, object, "channelmix.max-volume");
  fill_bool(node.channel_mix_normalize, object, "channelmix.normalize");
  fill_bool(node.channel_mix_lock_volumes, object, "channelmix.lock-volumes");
  fill_bool(node.channel_mix_mix_lfe, object, "channelmix.mix-lfe");
  fill_bool(node.channel_mix_upmix, object, "channelmix.upmix");
  fill_uint64(node.channel_mix_lfe_cutoff, object, "channelmix.lfe-cutoff");
  fill_uint64(node.channel_mix_fc_cutoff, object, "channelmix.fc-cutoff");
  fill_double(node.channel_mix_rear_delay, object, "channelmix.rear-delay");
  fill_double(node.channel_mix_stereo_widen, object, "channelmix.stereo-widen");
  fill_double(node.channel_mix_hilbert_taps, object, "channelmix.hilbert-taps");

  fill_uint64(node.client_id, object, PW_KEY_CLIENT_ID);

  fill_uint64(node.device_id, object, PW_KEY_DEVICE_ID);
  fill_uint64(node.device_plugged, object, PW_KEY_DEVICE_PLUGGED);
  fill_uint64(node.dither_noise, object, "dither.noise");
  fill_uint64(node.latency_internal_rate, object, "latency.internal-rate");
  fill_uint64(node.latency_internal_ns, object, "latency.internal-ns");
  fill_uint64(node.link_id, object, PW_KEY_LINK_ID);
  fill_uint64(node.link_input_node, object, PW_KEY_LINK_INPUT_NODE);
  fill_uint64(node.link_output_node, object, PW_KEY_LINK_OUTPUT_NODE);
  fill_uint64(node.link_input_port, object, PW_KEY_LINK_INPUT_PORT);
  fill_uint64(node.link_output_port, object, PW_KEY_LINK_OUTPUT_PORT);
  fill_bool(node.link_passive, object, PW_KEY_LINK_PASSIVE);
  fill_bool(node.link_feedback, object, PW_KEY_LINK_FEEDBACK);
  fill_bool(node.link_async, object, PW_KEY_LINK_ASYNC);
  fill_bool(node.monitor_channel_volumes, object, "monitor.channel-volumes");
  fill_uint64(node.node_id, object, PW_KEY_NODE_ID);
  fill_bool(node.node_lock_quantum, object, PW_KEY_NODE_LOCK_QUANTUM);
  fill_uint64_with_existence(node.node_force_quantum,
                             node.has_node_force_quantum, object,
                             PW_KEY_NODE_FORCE_QUANTUM);
  fill_uint64_with_existence(node.node_rate, node.has_node_rate, object,
                             PW_KEY_NODE_RATE);
  fill_bool(node.node_lock_rate, object, PW_KEY_NODE_LOCK_RATE);
  fill_uint64_with_existence(node.node_force_rate, node.has_node_force_rate,
                             object, PW_KEY_NODE_FORCE_RATE);
  fill_bool(node.node_always_process, object, PW_KEY_NODE_ALWAYS_PROCESS);
  fill_bool(node.node_want_driver, object, PW_KEY_NODE_WANT_DRIVER);
  fill_bool(node.node_pause_on_idle, object, PW_KEY_NODE_PAUSE_ON_IDLE);
  fill_bool(node.node_suspend_on_idle, object, PW_KEY_NODE_SUSPEND_ON_IDLE);
  fill_bool(node.node_autoconnect, object, PW_KEY_NODE_AUTOCONNECT);
  fill_bool(node.node_exclusive, object, PW_KEY_NODE_EXCLUSIVE);
  fill_bool(node.node_dont_reconnect, object, PW_KEY_NODE_DONT_RECONNECT);
  fill_bool(node.node_passive, object, PW_KEY_NODE_PASSIVE);
  fill_bool(node.node_disabled, object, "node.disabled");
  fill_uint64(node.object_id, object, PW_KEY_OBJECT_ID);
  fill_uint64(node.object_serial, object, PW_KEY_OBJECT_SERIAL);
  fill_bool(node.object_linger, object, PW_KEY_OBJECT_LINGER);
  fill_uint64(node.pipewire_sec_pid, object, PW_KEY_SEC_PID);
  fill_uint64(node.pipewire_sec_uid, object, PW_KEY_SEC_UID);
  fill_uint64(node.pipewire_sec_gid, object, PW_KEY_SEC_GID);
  fill_uint64(node.port_id, object, PW_KEY_PORT_ID);
  fill_bool(node.port_physical, object, PW_KEY_PORT_PHYSICAL);
  fill_uint64_with_existence(node.priority_driver, node.has_priority_driver,
                             object, PW_KEY_PRIORITY_DRIVER);
  fill_uint64_with_existence(node.priority_session, node.has_priority_session,
                             object, PW_KEY_PRIORITY_SESSION);

  fill_uint64(node.resample_quality, object, "resample.quality");
  fill_bool(node.resample_disable, object, "resample.disable");
  fill_bool(node.resample_peaks, object, "resample.peaks");
  fill_bool(node.resample_prefill, object, "resample.prefill");
  fill_bool(node.stream_dont_remix, object, PW_KEY_STREAM_DONT_REMIX);
}
