namespace SonicEddy.Services.StreamingControl;

// Naming convention for pw-video-compositor instances, mirroring node_name()
// in pw-video-compositor/src/main.cpp 1:1: "se.video-compositor.<instance>.*"
// - lets two (or more) compositor processes coexist in the same PipeWire
// graph for the T-bar M/E switcher prototype.
public static class CompositorInstanceNames
{
    public const string A = "A";
    public const string B = "B";

    public static readonly string[] All = [A, B];

    public static string OutputNode(string instance) => $"se.video-compositor.{instance}.out";

    public static string InputNode(string instance, int index) => $"se.video-compositor.{instance}.in{index}";
}
