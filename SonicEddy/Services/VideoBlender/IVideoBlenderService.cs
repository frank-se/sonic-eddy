using System;
using System.Threading.Tasks;
using Fr.Sonic.Compositor;

namespace SonicEddy.Services.VideoBlender;

// Finds the video-blender's "se.video-blender.out" node (an external
// process SonicEddy did not create - same discovery idiom as
// IStreamingControlService) and owns a VideoBlenderClient for it once
// found. The blender may not be running yet, or may be restarted later -
// Client is null and ConnectionChanged fires whenever that availability
// changes.
public interface IVideoBlenderService
{
    VideoBlenderClient? Client { get; }
    event Action? ConnectionChanged;
    Task InitializeAsync();
}
