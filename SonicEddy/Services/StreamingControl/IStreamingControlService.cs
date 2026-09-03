using System;
using System.Threading.Tasks;
using Fr.Sonic.Compositor;

namespace SonicEddy.Services.StreamingControl;

// Finds pw-video-compositor's "se.video-compositor.out" node (an external
// process SonicEddy did not create) and owns a CompositorClient for it once
// found. The compositor may not be running yet when SonicEddy starts, or
// may be restarted later - Client is null and ConnectionChanged fires
// whenever that availability changes.
public interface IStreamingControlService
{
    CompositorClient? Client { get; }
    event Action? ConnectionChanged;
    Task InitializeAsync();
    Task<SceneFileConfig?> LoadSceneFileAsync(string path);

    // Shared per-object "current value" cache - see ObjectState. `baseline`
    // seeds a first-time lookup from the scene file; subsequent calls for
    // the same (sceneIndex, objectIndex) ignore it and return the existing
    // (possibly already-modified) state.
    ObjectState GetOrCreateObjectState(int sceneIndex, int objectIndex, SceneFileObject baseline);

    // Applies `mutate` to the object's state and raises ObjectStateChanged -
    // the only way callers should modify an ObjectState, so every writer
    // (the window, the gamepad dispatcher) notifies every other observer.
    void UpdateObjectState(int sceneIndex, int objectIndex, Action<ObjectState> mutate);

    event Action<int, int>? ObjectStateChanged; // (sceneIndex, objectIndex)

    // Which object is currently "the one being controlled" - announced by
    // whichever driver changed it (a row click in the Streaming Controls
    // window, or the gamepad's Next/Previous Object), so every other driver
    // and every open window can show the same selection instead of each
    // tracking its own, invisible-to-others notion of "selected".
    (int SceneIndex, int FlatIndex)? CurrentSelection { get; }
    void SelectObject(int sceneIndex, int flatIndex);
    event Action? SelectionChanged;
}
