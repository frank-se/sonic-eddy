using System;
using System.Collections.Generic;
using System.Windows.Input;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class OutputChannelViewModel(
    string text,
    ICommand selectChannelCommand,
    Node captureNode,
    bool isMaster)
    : ReactiveObject, IOutputChannel,
        IDisposable, IRoutingTarget
{
    public IPanAndVolume PanAndVolume { get; } =
        new PanAndVolumeViewModel(captureNode);

    public string Name { get; } = text;

    public string Text { get; } = text;

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsMaster
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = isMaster;

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
}