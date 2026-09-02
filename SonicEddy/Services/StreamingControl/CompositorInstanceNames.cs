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
    // se.mixer-overview, a future generated stream) is purely a
    // CameraRouterService assignment, not something this code special-cases.
    public const int InputSlotCount = 10;

    // Deliberately NOT in All below - All drives CameraRouterService's
    // fan-out to the two T-bar M/E panels specifically. The downstream
    // node's camera-type input (video in) is exclusively fed by the
    // video-blender's output via a manual pw-link, never a physical camera
    // or the mixer-overview.
    public const string Downstream = "Downstream";

    public static readonly string[] All = [A, B];

    public static string OutputNode(string instance) => $"se.video-compositor.{instance}.out";

    public static string InputNode(string instance, int index) => $"se.video-compositor.{instance}.in{index}";
}
