using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Modules.Models;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;
using Splat;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MicChannelViewModel : ChannelViewModelBase, IMicChannel
{
    public const int MicLayerId = 2;

    public MicChannelViewModel(
        string text,
        ICommand selectChannelCommand,
        MicChannel micChannel,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        IAppDataService appDataService,
        IMixerService mixerService,
        IMonitoringService monitoringService,
        IMidiControllerSetupService midiControllerSetupService)
        : base(0, text, selectChannelCommand,
            micChannel.InputLoopback, micChannel.InputLoopback,
            micChannel.FilterChain, micChannel.FilterGraph,
            [], null,
            appDataService, mixerService, monitoringService,
            true, MicLayerId, midiControllerSetupService,
            ChannelType.Channel)
    {
        MicChannel = micChannel;
        AudioFromRoutingTargets = audioFromRoutingTargets;
        IsFilterMidiControlled = true;

        this.WhenAnyValue(x => x.SelectedAudioFromRoutingTarget)
            .Subscribe(routingTarget =>
            {
                if (routingTarget?.Channel is not InputChannelViewModel channel)
                    return;
                MicChannel.InputLoopback.CaptureNode.OverrideTargetObject(
                    channel.PlaybackNodeObjectSerial.ToString());
            })
            .DisposeWith(Disposables);
    }

    public MicChannel MicChannel { get; }

    public ObservableCollection<IRoutingTarget>
        AudioFromRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioFromRoutingTarget
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public override async Task AddFilterAction()
    {
        var externalEffects =
            Locator.Current.GetService<IExternalEffectService>()!;
        var dialogViewModel = new AddFilterChainViewModel(_appDataService,
            externalEffects);
        var dialog = new AddFilterChainView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (!dialogViewModel.DialogResult) return;
        if (dialogViewModel.SelectedFilterGraph is { } graph)
        {
            ExternalEffectId = null;
            FilterGraph = graph;
            FilterChain = await _mixerService.AddFilterToMicChannel(graph);
        }
        else if (dialogViewModel.SelectedExternalEffect is { } effect)
        {
            FilterChain = null;
            FilterGraph = null;
            var processor =
                await _mixerService.AddExternalEffectToMicChannel(
                    effect.Config.Id);
            ExternalEffectId = processor?.ExternalEffectId;
            HasFilter = ExternalEffectId is not null;
            Parameters = null;
        }
    }
}
