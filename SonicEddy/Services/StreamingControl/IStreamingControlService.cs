using System;
using System.Threading.Tasks;
using Fr.Sonic.Compositor;

namespace SonicEddy.Services.StreamingControl;

// Finds pw-video-compositor's "se.video-compositor.out" node (an external
// process SonicEddy did not create - see CameraRouterService for the same
// discovery pattern applied to camera nodes) and owns a CompositorClient
// for it once found. The compositor may not be running yet when SonicEddy
// starts, or may be restarted later - Client is null and ConnectionChanged
// fires whenever that availability changes.
public interface IStreamingControlService
{
    CompositorClient? Client { get; }
    event Action? ConnectionChanged;
    Task InitializeAsync();
    Task<SceneFileConfig?> LoadSceneFileAsync(string path);
}
