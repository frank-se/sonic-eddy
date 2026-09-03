namespace SonicEddy.Services.StreamingControl;

// Naming convention for pw-video-compositor instances, mirroring node_name()
// in pw-video-compositor/src/main.cpp 1:1: "se.video-compositor.<instance>.*"
// - lets two (or more) compositor processes coexist in the same PipeWire
// graph for the T-bar M/E switcher prototype.
public static class CompositorInstanceNames
{
    public const string A = "A";
    public const string B = "B";

    // Total shared input slots (se.video-compositor.<instance>.in0..in9) -
    // stable and uniform, populated per pw-video-compositor's --inputs
    // camera-definition file. What feeds any given slot (physical camera,
    // se.mixer-overview, a future generated stream) is a target_object
    // entry in that --inputs file, not something this code special-cases.
    public const int InputSlotCount = 10;

    // Deliberately NOT in All below - All is the two T-bar M/E panels
    // specifically. Downstream isn't a pw-video-compositor instance at all -
    // it's the separate downstream-compositor binary (see pw-video-compositor/
    // src/downstream_main.cpp), a different node namespace entirely. This
    // constant survives only as the Splat DI key for its
    // IStreamingControlService registration.
    public const string Downstream = "Downstream";

    public static readonly string[] All = [A, B];

    public static string OutputNode(string instance) => $"se.video-compositor.{instance}.out";

    public static string InputNode(string instance, int index) => $"se.video-compositor.{instance}.in{index}";

    // downstream-compositor's own fixed node names - no --instance-name
    // concept, since there's exactly one Downstream, ever. See
    // downstream_main.cpp's connect_video_stream call sites.
    public const string DownstreamOutputNode = "se.downstream.out";
    public const string DownstreamBaseInputNode = "se.downstream.base";
}
