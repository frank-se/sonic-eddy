using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public abstract class ChannelViewModelBase : ViewModelBase, IChannel,
    IDisposable
{
    protected readonly CompositeDisposable Disposables = new();

    protected ChannelViewModelBase(ulong channelId, string text,
        ICommand selectChannelCommand, LoopbackModule inputLoopback,
        LoopbackModule outputLoopback,
        FilterChain? filterChain,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget? selectedAudioToRoutingTarget,
        IAppDataService appDataService,
        IMixerService mixerService,
        IMonitoringService monitoringService)
    {
        AudioToRoutingTargets = audioToRoutingTargets;
        SelectChannelCommand = selectChannelCommand;
        InputLoopback = inputLoopback;
        OutputLoopback = outputLoopback;
        Text = text;
        ChannelId = channelId;
        FilterChain = filterChain;
        SelectedAudioToRoutingTarget = selectedAudioToRoutingTarget;
        AppDataService = appDataService;
        MixerService = mixerService;

        PanAndVolume =
            new PanAndVolumeViewModel(OutputLoopback.PlaybackNode,
                monitoringService);

        this.WhenAnyValue(x => x.SelectedAudioToRoutingTarget)
            .Subscribe(routingTarget =>
            {
                if (routingTarget is null) return;

                switch (routingTarget.Channel)
                {
                    case ChannelViewModelBase channel:
                        OutputLoopback.PlaybackNode.OverrideTargetObject(
                            channel.InputLoopback.CaptureNode.ObjectSerial
                                .ToString());
                        break;
                    case OutputChannelViewModel output:
                        OutputLoopback.PlaybackNode.OverrideTargetObject(
                            output.CaptureNodeObjectSerial.ToString());
                        break;
                }
            })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.Parameters)
            .Subscribe(parameters =>
            {
                FirstPluginParameters?.Clear();
                SecondPluginParameters?.Clear();
                ThirdPluginParameters?.Clear();

                if (parameters is null) return;

                var index = 0;
                foreach (var parameterCollection in parameters.Take(3))
                {
                    switch (index)
                    {
                        case 0:
                            FirstPluginText = parameterCollection.Name;
                            FirstPluginParameters?.AddRange(parameterCollection
                                .Parameters);
                            break;
                        case 1:
                            SecondPluginText = parameterCollection.Name;
                            SecondPluginParameters?.AddRange(parameterCollection
                                .Parameters);
                            break;
                        case 2:
                            ThirdPluginText = parameterCollection.Name;
                            ThirdPluginParameters?.AddRange(parameterCollection
                                .Parameters);
                            break;
                    }

                    index++;
                }
            })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.FilterChain)
            .Subscribe(chain =>
            {
                if (chain is null)
                {
                    HasFilter = false;
                    Parameters = null;
                    return;
                }

                // ReSharper disable once MergeIntoPattern
                // DO NOT CHANGE TO PATTERN
                // OTHERWISE ACCESS TO RESULT MIGHT BE BLOCKING!
                if (!chain.CaptureNode.Params.IsCompleted ||
                    chain.CaptureNode.Params.Result is null ||
                    !chain.CaptureNode.PropertyInfos.IsCompleted)
                    return;

                Parameters =
                    ConversionHelper.GetCollectionFromFilterChainParams(
                        chain.CaptureNode.Params.Result,
                        chain.CaptureNode.PropertyInfos.Result,
                        chain.CaptureNode);

                HasFilter = true;
            })
            .DisposeWith(Disposables);
    }

    protected readonly LoopbackModule InputLoopback;
    protected readonly LoopbackModule OutputLoopback;
    protected readonly IAppDataService AppDataService;
    protected readonly IMixerService MixerService;

    public FilterChain? FilterChain
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ulong ChannelId
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Text
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<ParameterCollection>? Parameters
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand SelectChannelCommand { get; set; }

    public void OnSelectChannel() =>
        SelectChannelCommand.Execute(this);

    public bool HasFilter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public async Task AddFilterAction()
    {
        var dialogViewModel = new AddFilterChainViewModel(AppDataService);
        var dialog = new AddFilterChainView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel is
            { DialogResult: true, SelectedFilterGraph: not null })
        {
            var channelStrip = await MixerService.AddFilterToChannelStrip(
                ChannelId,
                dialogViewModel.SelectedFilterGraph);
            FilterChain = channelStrip.FilterChain;
        }
    }

    public void DeleteFilterAction()
    {
    }

    public ObservableCollection<IParameter>? FirstPluginParameters { get; } =
        [];

    public string FirstPluginText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<IParameter>? SecondPluginParameters { get; } =
        [];

    public string SecondPluginText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<IParameter>? ThirdPluginParameters { get; } =
        [];

    public string ThirdPluginText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public IPanAndVolume PanAndVolume { get; }

    public ObservableCollection<IRoutingTarget>
        AudioToRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioToRoutingTarget
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    protected virtual void Dispose(bool disposing)
    {
        Disposables.Dispose();
        if (PanAndVolume is PanAndVolumeViewModel panAndVolume)
            panAndVolume.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}