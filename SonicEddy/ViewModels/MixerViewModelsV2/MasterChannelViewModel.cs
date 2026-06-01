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
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MasterChannelViewModel : ChannelViewModelBase, IMasterChannel
{
    private readonly Node? _globalMasterPlaybackNode;

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
        ITraktorZ1SetupService traktorZ1SetupService,
        Node? globalMasterPlaybackNode = null)
        : base(channelId, text, selectChannelCommand,
            inputLoopback, outputLoopback, filterChain, filterGraph,
            audioToRoutingTargets, selectedAudioToRoutingTarget,
            appDataService, mixerService, monitoringService,
            true, layerId, midiControllerSetupService, ChannelType.Channel)
    {
        _globalMasterPlaybackNode = globalMasterPlaybackNode;
        MasterChannel = masterChannel;
        Looper = new LooperSectionViewModel(
            masterChannel.PreFxLooper, masterChannel.PostFxLooper);
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
        var dialogViewModel = new AddFilterChainViewModel(_appDataService);
        var dialog = new AddFilterChainView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel is not
            { DialogResult: true, SelectedFilterGraph: not null })
            return;

        FilterChain = await _mixerService.AddFilterToMasterChannel(
            _layerId, dialogViewModel.SelectedFilterGraph);
        FilterGraph = dialogViewModel.SelectedFilterGraph;
    }

    protected override void ApplyAudioRoutingTarget(IRoutingTarget? routingTarget)
    {
        if (routingTarget is null) return;
        var node = _globalMasterPlaybackNode;
        if (node is null) { base.ApplyAudioRoutingTarget(routingTarget); return; }
        switch (routingTarget.Channel)
        {
            case OutputChannelViewModel output:
                node.OverrideTargetObject(output.CaptureNodeObjectSerial.ToString());
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        Looper.Dispose();
        base.Dispose(disposing);
    }
}
