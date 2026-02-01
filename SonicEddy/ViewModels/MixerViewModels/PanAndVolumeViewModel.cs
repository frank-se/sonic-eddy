using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Audio;

namespace SonicEddy.ViewModels.MixerViewModels;

public class PanAndVolumeViewModel : ReactiveObject, IDisposable
{
    private const float Tolerance = 0.0001f;

    private readonly CompositeDisposable _disposables = new();
    private readonly LoopbackModule _loopbackModule;

    public PanAndVolumeViewModel(LoopbackModule loopbackModule)
    {
        _loopbackModule = loopbackModule;

        loopbackModule.PlaybackNode.PropertiesChanged += UpdateVolumeAndPan;

        this.WhenAnyValue(x => x.Volume)
            .Skip(2)
            .Subscribe(volume => UpdateGains(Pan, volume))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Pan)
            .Skip(2)
            .Subscribe(pan => UpdateGains(pan, Volume))
            .DisposeWith(_disposables);

        if (loopbackModule.PlaybackNode.Properties.IsCompleted)
        {
            UpdateVolumeAndPan(loopbackModule.PlaybackNode.Properties.Result);
        }
    }

    private bool _updateFromModel = false;

    private void UpdateGains(float pan, float volume)
    {
        var propertiesTask = _loopbackModule.PlaybackNode.Properties;
        if (!propertiesTask.IsCompleted) return;
        if (propertiesTask.Result is null) return;
        if (_updateFromModel) return;

        var properties = propertiesTask.Result;
        var currentGains = properties.Channels.Select(c => c.Volume).ToArray();
        if (currentGains.Length != 2) return;

        var rawGains =
            Audio.Pan.GetGainsFromPanAndVolume(Pan, Volume);

        var newGains = rawGains.BoostToExternal();

        if (Math.Abs(newGains[0] - currentGains[0]) > Tolerance ||
            Math.Abs(newGains[1] - currentGains[1]) > Tolerance)
        {
            _loopbackModule.PlaybackNode.SetVolumes(newGains);
        }
    }

    private void UpdateVolumeAndPan(Properties? properties)
    {
        if (properties is null) return;

        var rawGains = properties.Channels.Select(c => (double)c.Volume)
            .ToArray();
        if (rawGains.Length != 2) return;

        var internalGains = rawGains.AttenuateFromExternal();

        var newPanAndVolume =
            Audio.Pan.GetPanAndVolumeFromGains(internalGains[0],
                internalGains[1]);

        _updateFromModel = true;
        Volume = (float)newPanAndVolume.Volume;
        Pan = (float)newPanAndVolume.Pan;
        _updateFromModel = false;
    }

    public float Volume
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float Pan
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void Dispose()
    {
        _loopbackModule.PlaybackNode.PropertiesChanged -= UpdateVolumeAndPan;
        _disposables.Dispose();
    }
}