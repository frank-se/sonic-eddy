using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.PInvoke;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using SonicEddy.Services.TraktorZ1;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;
using Splat;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MasterChannelViewModel : ChannelViewModelBase, IMasterChannel
{
    public MasterChannelViewModel(
        ulong channelId,
        string text,
        ICommand selectChannelCommand,
        TwoNodePipewireModule inputLoopback,
        TwoNodePipewireModule outputLoopback,
        FilterChain? filterChain,
        FilterGraph? filterGraph,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget? selectedAudioToRoutingTarget,
        IAppDataService appDataService,
        IMixerService mixerService,
        MasterChannel masterChannel,
        IMonitoringService monitoringService,
        int layerId,
        IMidiControllerSetupService midiControllerSetupService,
        ITraktorZ1SetupService traktorZ1SetupService)
        : base(channelId, text, selectChannelCommand,
            inputLoopback, outputLoopback, filterChain, filterGraph,
            audioToRoutingTargets, selectedAudioToRoutingTarget,
            appDataService, mixerService, monitoringService,
            true, layerId, midiControllerSetupService, ChannelType.Channel)
    {
        MasterChannel = masterChannel;
        Looper = new LooperSectionViewModel(
            masterChannel.PreFxLooper, masterChannel.PostFxLooper,
            midiControllerSetupService, ChannelType.Master, (ulong)layerId);
        IsFilterMidiControlled = true;

        var side = layerId == 0 ? TraktorZ1Side.Left : TraktorZ1Side.Right;

        this.WhenAnyValue(x => x.Parameters)
            .Subscribe(parameters =>
            {
                traktorZ1SetupService.ClearFilterSections(side);
                if (parameters is null) return;

                foreach (var (collection, sectionIdx) in
                         parameters.Select((c, i) => (c, i)))
                {
                    var node = FilterChain?.CaptureNode;
                    if (node is null) continue;

                    foreach (var parameter in collection.Parameters)
                        traktorZ1SetupService.AddFilterParameter(
                            side, sectionIdx, node,
                            parameter.FullyQualifiedName,
                            parameter.Minimum, parameter.Maximum);
                }
            })
            .DisposeWith(Disposables);
    }

    public MasterChannel MasterChannel { get; }
    public LooperSectionViewModel Looper { get; }

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
            FilterChain = await _mixerService.AddFilterToMasterChannel(
                _layerId, graph);
        }
        else if (dialogViewModel.SelectedExternalEffect is { } effect)
        {
            FilterChain = null;
            FilterGraph = null;
            var processor =
                await _mixerService.AddExternalEffectToMasterChannel(
                    _layerId, effect.Config.Id);
            ExternalEffectId = processor?.ExternalEffectId;
            HasFilter = ExternalEffectId is not null;
            Parameters = null;
        }
    }

    protected override void ApplyAudioRoutingTarget(IRoutingTarget? routingTarget)
    {
        if (routingTarget?.Channel is not OutputChannelViewModel output) return;
        _mixerService.SetGlobalMasterOutputTarget(output.CaptureNodeObjectSerial);
    }

    protected override void Dispose(bool disposing)
    {
        Looper.Dispose();
        base.Dispose(disposing);
    }
}
