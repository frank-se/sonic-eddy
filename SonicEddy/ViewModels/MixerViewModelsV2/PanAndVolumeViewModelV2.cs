using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using DynamicData.Binding;
using Fr.Pw.Monitoring.Monitoring;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Props;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class PanAndVolumeViewModelV2 : ReactiveObject, IPanAndVolume,
    IDisposable
{
    private readonly CompositeDisposable _disposable = new();

    private readonly IMonitoringService _monitoringService;

    public PanAndVolumeViewModelV2(Node node,
        IMonitoringService monitoringService)
    {
        _node = node;
        _monitoringService = monitoringService;

        _monitoringService.StartMonitoring(node);
        _monitoringService.Updated += OnMonitoringUpdate;

        // ReSharper disable once MergeIntoPattern
        // DO NOT CHANGE TO PATTERN
        // OTHERWISE ACCESS TO RESULT MIGHT BE BLOCKING!
        if (node.Properties.IsCompleted &&
            node.Properties.Result is not null)
        {
            var properties = node.Properties.Result;
            OnPropertiesChanged(properties);
        }

        _node.PropertiesChanged += OnPropertiesChanged;

        this.WhenAnyValue(x => x.Volume)
            .Subscribe(volume =>
            {
                SetNodeVolumesFromPanAndVolume(Pan, volume);
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.Pan)
            .Subscribe(pan => { SetNodeVolumesFromPanAndVolume(pan, Volume); })
            .DisposeWith(_disposable);
    }

    private void OnMonitoringUpdate(MonitoringUpdate update)
    {
        if (update.ObjectSerial != _node.ObjectSerial) return;
        LeftPeak = update.Peaks[0];
        RightPeak = update.Peaks[1];
        LeftAverage = update.Averages[0];
        RightAverage = update.Averages[1];
    }

    private bool _internalChanges = false;
    
    private void SetNodeVolumesFromPanAndVolume(double pan, double volume)
    {
        if (_internalChanges) return;
        var internalVolumes = Audio.Pan.GetGainsFromPanAndVolume(pan, volume);
        var externalVolumes = Audio.Pan.BoostToExternal(internalVolumes);
        _node.SetVolumes(externalVolumes);
    }

    private void OnPropertiesChanged(Properties? properties) =>
        SetVolumeAndPanFromProperties(properties);

    private void SetVolumeAndPanFromProperties(Properties? properties)
    {
        if (properties is null) return;

        var externalVolumes =
            properties.Channels.Select(c => (double)c.Volume)
                .ToArray();

        if (externalVolumes.Length < 2)
        {
            Pan = 0.0f;
            Volume = 0.0f;
            return;
        }

        var internalVolumes =
            Audio.Pan.AttenuateFromExternal(externalVolumes);

        var (pan, volume) =
            Audio.Pan.GetPanAndVolumeFromGains(internalVolumes[0],
                internalVolumes[1]);

        _internalChanges = true;
        Volume = volume;

        if (Volume == 0.0f)
        {
            _internalChanges = false;
            return;
        }

        Pan = pan;
        _internalChanges = false;
    }

    private readonly Node _node;

    public double Volume
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Pan
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float LeftAverage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float RightAverage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float LeftPeak
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float RightPeak
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void Dispose()
    {
        _node?.PropertiesChanged -= OnPropertiesChanged;
        if (_node is not null)
            _monitoringService.StopMonitoring(_node);

        _monitoringService.Updated -= OnMonitoringUpdate;
        _disposable.Dispose();
    }
}