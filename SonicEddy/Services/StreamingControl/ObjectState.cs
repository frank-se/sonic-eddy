namespace SonicEddy.Services.StreamingControl;

// The "current" value of everything object_params can control for one
// object. The compositor never echoes object_params back (see
// project_scene_file_format memory), so this - not the scene file, not
// the compositor - is the one source of truth both the Streaming Controls
// window and the gamepad dispatcher read/write, via
// IStreamingControlService.GetOrCreateObjectState/UpdateObjectState.
public sealed class ObjectState
{
    public int X;
    public int Y;
    public bool Visible = true;
    public bool FlipHorizontal;
    public bool FlipVertical;
    public float RedGain = 1.0f;
    public float GreenGain = 1.0f;
    public float BlueGain = 1.0f;
}
