#pragma once

#include <cstdint>
#include <wp/proxy-interfaces.h>

namespace models::objects {
enum class wireplumber_object_type {
  node = 1,
  port = 2,
  link = 3,
  device = 4,
  client = 5,
};

struct wireplumber_object {
  wireplumber_object_type type = wireplumber_object_type::node;

  std::uint64_t object_id = 0;
  std::uint64_t object_serial = 0;
  bool object_linger = false;
  const char *object_path = nullptr;

  bool alsa_use_acp = false;
  bool alsa_udev_expose_busy = false;

  const char *application_name = nullptr;
  uint64_t application_process_id = 0;

  bool api_acp_auto_profile = true;
  bool api_acp_auto_port = true;

  uint64_t api_acp_probe_rate = 0;
  bool has_api_acp_probe_rate = false;

  uint64_t api_acp_pro_channels = 0;
  bool has_api_acp_pro_channels = false;

  const char *api_alsa_path = nullptr;
  bool api_alsa_use_ucm = true;
  bool api_alsa_soft_mixer = false;
  bool api_alsa_disable_mixer_path = false;
  bool api_alsa_ignore_db = false;
  bool api_alsa_split_enable = false;

  uint64_t api_alsa_period_size = 0;
  bool has_api_alsa_period_size = false;

  uint64_t api_alsa_period_num = 0;
  bool has_api_alsa_period_num = false;

  uint64_t api_alsa_headroom = 0;
  uint64_t api_alsa_start_delay = 0;
  bool api_alsa_disable_mmap = false;
  bool api_alsa_disable_batch = false;
  bool api_alsa_use_chmap = false;
  bool api_alsa_multi_rate = true;
  bool api_alsa_htimestamp = false;

  uint64_t api_alsa_htimestamp_max_errors = 0;
  bool has_api_alsa_htimestamp_max_errors = false;

  bool api_alsa_disable_tsched = false;
  bool api_alsa_auto_link = false;

  const char *audio_channel = nullptr;

  uint64_t audio_channels = 0;
  bool has_audio_channels = false;

  uint64_t audio_rate = 0;
  bool has_audio_rate = false;

  const char *audio_format = nullptr;
  const char *audio_allowed_rates = "[]";
  const char *audio_position = nullptr;

  bool channel_mix_disable = false;
  double channel_mix_min_volume = 0.0;
  double channel_mix_max_volume = 10.0;
  bool channel_mix_normalize = false;
  bool channel_mix_lock_volumes = false;
  bool channel_mix_mix_lfe = true;
  bool channel_mix_upmix = true;
  const char *channel_mix_upmix_method = "psd";
  uint64_t channel_mix_lfe_cutoff = 150;
  uint64_t channel_mix_fc_cutoff = 12000;
  double channel_mix_rear_delay = 12.0;
  double channel_mix_stereo_widen = 0.0;
  double channel_mix_hilbert_taps = 0;


  const char *clock_name = nullptr;

  uint64_t client_id = 0;
  const char *client_api = nullptr;
  const char *client_name = nullptr;

  const char *debug_wav_path = "";

  uint64_t device_id = 0;
  const char *device_name = nullptr;
  uint64_t device_plugged = 0;
  const char *device_nick = nullptr;
  const char *device_description = nullptr;
  const char *device_serial = nullptr;
  const char *device_vendor_id = nullptr;
  const char *device_vendor_name = nullptr;
  const char *device_product_id = nullptr;
  const char *device_product_name = nullptr;
  const char *device_class = nullptr;
  const char *device_form_factor = nullptr;
  const char *device_icon = nullptr;
  const char *device_icon_name = nullptr;
  const char *device_intended_roles = nullptr;
  const char *device_profile_set = nullptr;
  const char *device_profile = nullptr;
  const char *device_subsystem = nullptr;

  uint64_t dither_noise = 0;
  const char *dither_method = "none";

  const char *format_dsp = nullptr;

  uint64_t latency_internal_rate = 0;
  uint64_t latency_internal_ns = 0;

  uint64_t link_id = 0;
  uint64_t link_input_node = 0;
  uint64_t link_output_node = 0;
  uint64_t link_input_port = 0;
  uint64_t link_output_port = 0;
  bool link_passive = false;
  bool link_feedback = false;
  bool link_async = false;
  const char *link_group = nullptr;

  const char *media_name = nullptr;
  const char *media_title = nullptr;
  const char *media_artist = nullptr;
  const char *media_copyright = nullptr;
  const char *media_software = nullptr;
  const char *media_language = nullptr;
  const char *media_filename = nullptr;
  const char *media_icon = nullptr;
  const char *media_icon_name = nullptr;
  const char *media_comment = nullptr;
  const char *media_date = nullptr;
  const char *media_format = nullptr;
  const char *media_type = nullptr;
  const char *media_category = nullptr;
  const char *media_role = nullptr;
  const char *media_class = nullptr;

  bool monitor_channel_volumes = false;

  uint64_t node_id = 0;
  const char *node_name = nullptr;
  const char *node_description = nullptr;
  uint64_t node_latency_numerator = 1024; // TODO
  uint64_t node_latency_denominator = 48000; // TODO
  bool node_lock_quantum = false;

  uint64_t node_force_quantum = 0;
  bool has_node_force_quantum = false;

  uint64_t node_rate = 0;
  bool has_node_rate = false;

  bool node_lock_rate = false;

  uint64_t node_force_rate = 0;
  bool has_node_force_rate = false;

  bool node_always_process = false;
  bool node_want_driver = true;
  bool node_pause_on_idle = false;
  bool node_suspend_on_idle = false;
  const char *node_loop_class = nullptr;
  const char *node_loop_name = nullptr;
  bool node_autoconnect = true;
  bool node_exclusive = false;
  bool node_dont_reconnect = false;
  bool node_passive = false;
  const char *node_link_group = nullptr;
  bool node_disabled = false;

  uint64_t pipewire_sec_pid = 0;
  uint64_t pipewire_sec_uid = 0;
  uint64_t pipewire_sec_gid = 0;
  const char *pipewire_sec_socket = nullptr;
  const char *pipewire_access = nullptr;
  const char *pipewire_client_access = nullptr;
  const char *pipewire_protocol = nullptr;

  const char *pmx_tag = nullptr;
  const char *pmx_purpose = nullptr;

  const char *port_name = nullptr;
  const char *port_alias = nullptr;
  const char *port_group = nullptr;
  uint64_t port_id = 0;
  const char *port_direction = nullptr;
  bool port_physical = false;

  uint64_t priority_driver = 0;
  bool has_priority_driver = false;

  uint64_t priority_session = 0;
  bool has_priority_session = false;

  uint64_t resample_quality = 4;
  bool resample_disable = false;
  bool resample_peaks = false;
  bool resample_prefill = false;

  bool stream_dont_remix = false;

  const char *target_object = nullptr;
};

void fill_node_from_object(wireplumber_object &node, WpPipewireObject *object);

wireplumber_object create_node_from_object(WpPipewireObject *object);
}
