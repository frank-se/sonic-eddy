using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using Fr.Sonic.Compositor;
using ReactiveUI;
using SonicEddy.Services.StreamingControl;

namespace SonicEddy.ViewModels.StreamingControlViewModels;

public sealed class StreamingControlViewModel : ViewModelBase, IDisposable
{
    private const int MaxScenes = 5;
    // Must match pw-video-compositor's kInputCount - index 2 is the
    // permanently-reserved mixer-overview input (see
    // MixerOverviewCompositorLinkService), not user-routable camera 0/1.
    private const int MaxCameraObjects = 3;
    private const int MaxImageObjects = 10;

    private readonly IStreamingControlService _service;
    private CompositorClient? _client;
    private SceneFileConfig? _activeSceneFile;
    private int _activeSceneIndex = -1;

    public StreamingControlViewModel(IStreamingControlService service)
    {
        _service = service;

        Scenes = BuildEmptySceneSlots();
        CameraObjects = BuildEmptyObjectSlots(MaxCameraObjects);
        ImageObjects = BuildEmptyObjectSlots(MaxImageObjects);

        _service.ConnectionChanged += OnConnectionChanged;
        AttachClient(_service.Client);
    }

    public bool IsConnected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<SceneSlotViewModel> Scenes
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<ObjectSlotViewModel> CameraObjects
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<ObjectSlotViewModel> ImageObjects
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObjectControlPanelViewModel? SelectedObjectControls
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private void OnConnectionChanged() =>
        Dispatcher.UIThread.Post(() => AttachClient(_service.Client));

    private void AttachClient(CompositorClient? client)
    {
        if (_client is not null)
            _client.ParamsChanged -= OnParamsChanged;

        _client = client;
        IsConnected = client is not null;
        _activeSceneFile = null;
        SelectedObjectControls?.Dispose();
        SelectedObjectControls = null;
        Scenes = BuildEmptySceneSlots();
        CameraObjects = BuildEmptyObjectSlots(MaxCameraObjects);
        ImageObjects = BuildEmptyObjectSlots(MaxImageObjects);

        if (client is null)
            return;

        client.ParamsChanged += OnParamsChanged;
        _ = LoadInitialAsync(client);
    }

    private async Task LoadInitialAsync(CompositorClient client)
    {
        var parameters = await client.GetParamsAsync();
        if (parameters is not null)
            Dispatcher.UIThread.Post(() => ApplyParams(parameters));
    }

    private void OnParamsChanged(CompositorParams parameters) =>
        Dispatcher.UIThread.Post(() => ApplyParams(parameters));

    private void ApplyParams(CompositorParams parameters)
    {
        var slots = new ObservableCollection<SceneSlotViewModel>();
        for (var i = 0; i < MaxScenes; ++i)
        {
            if (i < parameters.Scenes.Count)
            {
                var slot = new SceneSlotViewModel(i + 1, i, parameters.Scenes[i], SelectScene);
                slot.IsActive = i == parameters.ActiveSceneIndex;
                slots.Add(slot);
            }
            else
            {
                slots.Add(new SceneSlotViewModel(i + 1));
            }
        }
        Scenes = slots;
        _activeSceneIndex = parameters.ActiveSceneIndex;

        if (parameters.ActiveSceneIndex >= 0 &&
            parameters.ActiveSceneIndex < parameters.Scenes.Count)
            _ = LoadSceneObjectsAsync(parameters.Scenes[parameters.ActiveSceneIndex]);
    }

    private void SelectScene(int index, CompositorSceneInfo info) =>
        _client?.SetActiveSceneIndex(index);

    private async Task LoadSceneObjectsAsync(CompositorSceneInfo info)
    {
        var file = await _service.LoadSceneFileAsync(info.File);
        Dispatcher.UIThread.Post(() => RebuildObjectSlots(file));
    }

    private void RebuildObjectSlots(SceneFileConfig? file)
    {
        _activeSceneFile = file;
        SelectedObjectControls?.Dispose();
        SelectedObjectControls = null;

        var cameras = new ObservableCollection<ObjectSlotViewModel>();
        var images = new ObservableCollection<ObjectSlotViewModel>();

        if (file is not null)
        {
            for (var i = 0; i < file.Objects.Count; ++i)
            {
                var obj = file.Objects[i];
                if (obj.IsCamera && cameras.Count < MaxCameraObjects)
                    cameras.Add(new ObjectSlotViewModel(cameras.Count + 1, i, obj, SelectObject));
                else if (obj.IsImage && images.Count < MaxImageObjects)
                    images.Add(new ObjectSlotViewModel(images.Count + 1, i, obj, SelectObject));
            }
        }

        while (cameras.Count < MaxCameraObjects)
            cameras.Add(new ObjectSlotViewModel(cameras.Count + 1));
        while (images.Count < MaxImageObjects)
            images.Add(new ObjectSlotViewModel(images.Count + 1));

        CameraObjects = cameras;
        ImageObjects = images;
    }

    private void SelectObject(int flatIndex, SceneFileObject baseline)
    {
        if (_client is null || _activeSceneFile is null)
            return;

        SelectedObjectControls?.Dispose();
        SelectedObjectControls = new ObjectControlPanelViewModel(_service, _client, _activeSceneIndex,
            flatIndex, baseline, _activeSceneFile.CanvasWidth, _activeSceneFile.CanvasHeight);
    }

    private static ObservableCollection<SceneSlotViewModel> BuildEmptySceneSlots()
    {
        var slots = new ObservableCollection<SceneSlotViewModel>();
        for (var i = 0; i < MaxScenes; ++i)
            slots.Add(new SceneSlotViewModel(i + 1));
        return slots;
    }

    private static ObservableCollection<ObjectSlotViewModel> BuildEmptyObjectSlots(int count)
    {
        var slots = new ObservableCollection<ObjectSlotViewModel>();
        for (var i = 0; i < count; ++i)
            slots.Add(new ObjectSlotViewModel(i + 1));
        return slots;
    }

    public void Dispose()
    {
        _service.ConnectionChanged -= OnConnectionChanged;
        if (_client is not null)
            _client.ParamsChanged -= OnParamsChanged;
        SelectedObjectControls?.Dispose();
    }
}
