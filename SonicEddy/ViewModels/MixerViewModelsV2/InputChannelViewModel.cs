using System;
using System.Collections.Generic;
using System.Windows.Input;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class InputChannelViewModel(
    string text,
    ICommand selectChannelCommand,
    Node playbackNode, IMonitoringService monitoringService)
    : ReactiveObject, IInputChannel, IDisposable, IRoutingTarget
{
    public IChannel Channel => this;

    public IPanAndVolume PanAndVolume { get; } =
        new PanAndVolumeViewModel(playbackNode);

    private readonly Node _playbackNode = playbackNode;

    public string Text { get; } = text;

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<ParameterCollection>? Parameters => [];

    public ICommand SelectChannelCommand { get; set; } = selectChannelCommand;

    public void OnSelectChannel() =>
        SelectChannelCommand.Execute(this);

    public void Dispose()
    {
        if (PanAndVolume is PanAndVolumeViewModel panAndVolume)
            panAndVolume.Dispose();

        GC.SuppressFinalize(this);
    }

    public string Name { get; } = text;

    public ulong PlaybackNodeObjectSerial => _playbackNode.ObjectSerial;
}