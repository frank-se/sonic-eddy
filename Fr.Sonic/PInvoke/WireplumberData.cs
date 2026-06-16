// ReSharper disable InconsistentNaming

using System.Runtime.InteropServices;

namespace Fr.Sonic.PInvoke;

/// <summary>
/// Type of the wireplumber object
/// </summary>
public enum wireplumber_object_type
{
    /// <summary>
    /// A pipewire node
    /// </summary>
    node = 1,

    /// <summary>
    /// A pipewire port
    /// </summary>
    port = 2,

    /// <summary>
    /// A pipewire link
    /// </summary>
    link = 3,

    /// <summary>
    /// A pipewire device
    /// </summary>
    device = 4,

    /// <summary>
    /// A pipewire client
    /// </summary>
    client = 5
}

/*
 * This struct is used to pass data from the C++ to the C# side. The order of
 * the fields must match the order of the fields in the C++ struct.
 */
[StructLayout(LayoutKind.Sequential)]
internal struct WireplumberData()
{
    internal wireplumber_object_type type = wireplumber_object_type.node;

    internal ulong object_id = 0;
    internal ulong object_serial = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool object_linger = false;

    internal IntPtr object_path;

    [MarshalAs(UnmanagedType.U1)] internal bool alsa_use_acp = false;

    [MarshalAs(UnmanagedType.U1)] internal bool alsa_udev_expose_busy = false;

    internal IntPtr application_name;
    internal ulong application_process_id = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool api_acp_auto_profile = true;

    [MarshalAs(UnmanagedType.U1)] internal bool api_acp_auto_port = true;
    internal ulong api_acp_probe_rate = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_api_acp_probe_rate = false;
    internal ulong api_acp_pro_channels = 0;

    [MarshalAs(UnmanagedType.U1)]
    internal bool has_api_acp_pro_channels = false;

    internal IntPtr api_alsa_path;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_use_ucm = true;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_soft_mixer = false;

    [MarshalAs(UnmanagedType.U1)]
    internal bool api_alsa_disable_mixer_path = false;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_ignore_db = false;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_split_enable = false;

    internal ulong api_alsa_period_size = 0;

    [MarshalAs(UnmanagedType.U1)]
    internal bool has_api_alsa_period_size = false;

    internal ulong api_alsa_period_num = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_api_alsa_period_num = false;

    internal ulong api_alsa_headroom = 0;
    internal ulong api_alsa_start_delay = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_disable_mmap = false;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_disable_batch = false;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_use_chmap = false;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_multi_rate = true;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_htimestamp = false;

    internal ulong api_alsa_htimestamp_max_errors = 0;

    [MarshalAs(UnmanagedType.U1)]
    internal bool has_api_alsa_htimestamp_max_errors = false;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_disable_tsched = false;

    [MarshalAs(UnmanagedType.U1)] internal bool api_alsa_auto_link = false;

    internal IntPtr audio_channel;

    internal ulong audio_channels = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_audio_channels = false;

    internal ulong audio_rate = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_audio_rate = false;

    internal IntPtr audio_format;
    internal IntPtr audio_allowed_rates;
    internal IntPtr audio_position;

    [MarshalAs(UnmanagedType.U1)] internal bool channel_mix_disable = false;

    internal double channel_mix_min_volume = 0.0;
    internal double channel_mix_max_volume = 10.0;

    [MarshalAs(UnmanagedType.U1)] internal bool channel_mix_normalize = false;

    [MarshalAs(UnmanagedType.U1)]
    internal bool channel_mix_lock_volumes = false;

    [MarshalAs(UnmanagedType.U1)] internal bool channel_mix_mix_lfe = true;

    [MarshalAs(UnmanagedType.U1)] internal bool channel_mix_upmix = true;

    internal IntPtr channel_mix_upmix_method;
    internal ulong channel_mix_lfe_cutoff = 150;
    internal ulong channel_mix_fc_cutoff = 12000;
    internal double channel_mix_rear_delay = 12.0;
    internal double channel_mix_stereo_widen = 0.0;
    internal double channel_mix_hilbert_taps = 0;

    internal IntPtr clock_name;

    internal ulong client_id = 0;
    internal IntPtr client_api;
    internal IntPtr client_name;

    internal IntPtr debug_wav_path;

    internal ulong device_id = 0;
    internal IntPtr device_name;
    internal ulong device_plugged = 0;
    internal IntPtr device_nick;
    internal IntPtr device_description;
    internal IntPtr device_serial;
    internal IntPtr device_vendor_id;
    internal IntPtr device_vendor_name;
    internal IntPtr device_product_id;
    internal IntPtr device_product_name;
    internal IntPtr device_class;
    internal IntPtr device_form_factor;
    internal IntPtr device_icon;
    internal IntPtr device_icon_name;
    internal IntPtr device_intended_roles;
    internal IntPtr device_profile_set;
    internal IntPtr device_profile;
    internal IntPtr device_subsystem;

    internal ulong dither_noise = 0;
    internal IntPtr dither_method;

    internal IntPtr format_dsp;

    internal ulong latency_internal_rate = 0;
    internal ulong latency_internal_ns = 0;

    internal ulong link_id = 0;
    internal ulong link_input_node = 0;
    internal ulong link_output_node = 0;
    internal ulong link_input_port = 0;
    internal ulong link_output_port = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool link_passive = false;

    [MarshalAs(UnmanagedType.U1)] internal bool link_feedback = false;

    [MarshalAs(UnmanagedType.U1)] internal bool link_async = false;

    internal IntPtr link_group;

    internal IntPtr media_name;
    internal IntPtr media_title;
    internal IntPtr media_artist;
    internal IntPtr media_copyright;
    internal IntPtr media_software;
    internal IntPtr media_language;
    internal IntPtr media_filename;
    internal IntPtr media_icon;
    internal IntPtr media_icon_name;
    internal IntPtr media_comment;
    internal IntPtr media_date;
    internal IntPtr media_format;
    internal IntPtr media_type;
    internal IntPtr media_category;
    internal IntPtr media_role;
    internal IntPtr media_class;

    [MarshalAs(UnmanagedType.U1)] internal bool monitor_channel_volumes = false;

    internal ulong node_id = 0;
    internal IntPtr node_name;
    internal IntPtr node_description;
    internal ulong node_latency_numerator = 1024;
    internal ulong node_latency_denominator = 48000;

    [MarshalAs(UnmanagedType.U1)] internal bool node_lock_quantum = false;

    internal ulong node_force_quantum = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_node_force_quantum = false;

    internal ulong node_rate = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_node_rate = false;

    [MarshalAs(UnmanagedType.U1)] internal bool node_lock_rate = false;

    internal ulong node_force_rate = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_node_force_rate = false;

    [MarshalAs(UnmanagedType.U1)] internal bool node_always_process = false;

    [MarshalAs(UnmanagedType.U1)] internal bool node_want_driver = true;

    [MarshalAs(UnmanagedType.U1)] internal bool node_pause_on_idle = false;

    [MarshalAs(UnmanagedType.U1)] internal bool node_suspend_on_idle = false;

    internal IntPtr node_loop_class;
    internal IntPtr node_loop_name;

    [MarshalAs(UnmanagedType.U1)] internal bool node_autoconnect = true;

    [MarshalAs(UnmanagedType.U1)] internal bool node_exclusive = false;

    [MarshalAs(UnmanagedType.U1)] internal bool node_dont_reconnect = false;

    [MarshalAs(UnmanagedType.U1)] internal bool node_passive = false;

    internal IntPtr node_link_group;

    [MarshalAs(UnmanagedType.U1)] internal bool node_disabled = false;

    internal ulong pipewire_sec_pid = 0;
    internal ulong pipewire_sec_uid = 0;
    internal ulong pipewire_sec_gid = 0;
    internal IntPtr pipewire_sec_socket;
    internal IntPtr pipewire_access;
    internal IntPtr pipewire_client_access;
    internal IntPtr pipewire_protocol;

    internal IntPtr pmx_tag;
    internal IntPtr pmx_purpose;

    internal IntPtr port_name;
    internal IntPtr port_alias;
    internal IntPtr port_group;
    internal ulong port_id = 0;
    internal IntPtr port_direction;

    [MarshalAs(UnmanagedType.U1)] internal bool port_physical = false;

    internal ulong priority_driver = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_priority_driver = false;

    internal ulong priority_session = 0;

    [MarshalAs(UnmanagedType.U1)] internal bool has_priority_session = false;

    internal ulong resample_quality = 4;

    [MarshalAs(UnmanagedType.U1)] internal bool resample_disable = false;

    [MarshalAs(UnmanagedType.U1)] internal bool resample_peaks = false;

    [MarshalAs(UnmanagedType.U1)] internal bool resample_prefill = false;

    [MarshalAs(UnmanagedType.U1)] internal bool stream_dont_remix = false;

    internal IntPtr target_object;
}