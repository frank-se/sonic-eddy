using System;
using System.Collections.Generic;
using System.Windows.Input;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class OutputChannelViewModel(
    string text,
    ICommand selectChannelCommand,
    Node captureNode,
    IMonitoringService monitoringService)
    : ReactiveObject, IOutputChannel,
        IDisposable, IRoutingTarget
{
    public IChannel Channel => this;

    public IPanAndVolume PanAndVolume { get; } =
        new PanAndVolumeViewModel(captureNode);

    public string Name { get; } = text;

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

    public ulong CaptureNodeObjectSerial => captureNode.ObjectSerial;
    public ulong CaptureNodeObjectId => captureNode.ObjectId;
}