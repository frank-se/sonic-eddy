using Fr.Sonic.Model.Objects;

namespace Fr.Sonic.Compositor;

// Thin wrapper around video-blender's single Props control value
// ("blend_position", 0..1) - one-way control, no state read back (the
// blender never echoes Props), so this is much simpler than CompositorClient.
public sealed class VideoBlenderClient(Node node)
{
    public void SetBlendPosition(float value) =>
        node.SetParam("blend_position", Math.Clamp(value, 0f, 1f));
}
