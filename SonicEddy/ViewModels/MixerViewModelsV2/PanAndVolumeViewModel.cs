using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Fr.Pw.Monitoring.Monitoring;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Props;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class PanAndVolumeViewModel : ReactiveObject, IPanAndVolume, IDisposable
{
    private readonly CompositeDisposable _disposable = new();

    public PanAndVolumeViewModel(Node node)
    {
        _node = node;

        //_monitoringService.StartMonitoring(node);
        //_monitoringService.Updated += OnMonitoringUpdate;

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

    private void SetNodeVolumesFromPanAndVolume(double pan, double volume) =>
        _node.SetVolumes(
            Audio.Pan.BoostToExternal(
                Audio.Pan.GetGainsFromPanAndVolume(pan, volume)));

    private void OnPropertiesChanged(Properties? properties) =>
        SetVolumeAndPanFromProperties(properties);

    private void SetVolumeAndPanFromProperties(Properties? properties)
    {
        if (properties is null) return;

        var volumes =
            Audio.Pan.AttenuateFromExternal(
                properties.Channels.Select(c => (double)c.Volume)
                    .ToArray());

        if (volumes.Length < 2)
        {
            Pan = 0.0f;
            Volume = 0.0f;
        }
        else
        {
            var (pan, volume) =
                Audio.Pan.GetPanAndVolumeFromGains(volumes[0], volumes[1]);

            Pan = (float)pan;
            Volume = (float)volume;
        }
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
    } = 0.0f;

    public float RightAverage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 0.0f;

    public float LeftPeak
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 0.0f;

    public float RightPeak
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 0.0f;

    public void Dispose()
    {
        _node?.PropertiesChanged -= OnPropertiesChanged;
        _disposable.Dispose();
    }
}