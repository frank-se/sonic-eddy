using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class ChannelStripViewModel : ViewModelBase, IChannelStrip, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    private readonly LoopbackModule _inputLoopback;
    private readonly LoopbackModule _outputLoopback;
    private readonly List<LoopbackModule> _sendLoopbacks;
    private readonly IAppDataService _appDataService;
    private readonly IMixerService _mixerService;

    public ChannelStripViewModel(ulong channelId, string text,
        ICommand selectChannelCommand, LoopbackModule inputLoopback,
        LoopbackModule outputLoopback, List<LoopbackModule> sendLoopbacks,
        FilterChain? filterChain,
        List<InputChannelViewModel> audioFromRoutingTargets,
        List<OutputChannelViewModel> audioToRoutingTargets,
        OutputChannelViewModel selectedAudioToRoutingTarget,
        ChannelStrip channelStrip, IAppDataService appDataService,
        IMixerService mixerService)
    {
        SelectChannelCommand = selectChannelCommand;
        _inputLoopback = inputLoopback;
        _outputLoopback = outputLoopback;
        _sendLoopbacks = sendLoopbacks;
        Text = text;
        ChannelId = channelId;
        AudioFromRoutingTargets = new(audioFromRoutingTargets);
        AudioToRoutingTargets = new(audioToRoutingTargets);

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
            .DisposeWith(_disposables);

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
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Send1Trim)
            .Subscribe(trim => { SetVolumesForSend(0, trim); })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Send2Trim)
            .Subscribe(trim => { SetVolumesForSend(1, trim); })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Send3Trim)
            .Subscribe(trim => { SetVolumesForSend(2, trim); })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Send4Trim)
            .Subscribe(trim => { SetVolumesForSend(3, trim); })
            .DisposeWith(_disposables);

        FilterChain = filterChain;

        PanAndVolume =
            new PanAndVolumeViewModel(_outputLoopback.PlaybackNode);

        if (_sendLoopbacks.Count > 0)
            _sendLoopbacks[0].PlaybackNode.PropertiesChanged +=
                OnSend1PropertiesChanged;

        if (_sendLoopbacks.Count > 1)
            _sendLoopbacks[1].PlaybackNode.PropertiesChanged +=
                OnSend2PropertiesChanged;

        if (_sendLoopbacks.Count > 2)
            _sendLoopbacks[2].PlaybackNode.PropertiesChanged +=
                OnSend3PropertiesChanged;

        if (_sendLoopbacks.Count > 3)
            _sendLoopbacks[3].PlaybackNode.PropertiesChanged +=
                OnSend4PropertiesChanged;

        SelectedAudioToRoutingTarget = selectedAudioToRoutingTarget;
        ChannelStrip = channelStrip;
        _appDataService = appDataService;
        _mixerService = mixerService;
    }

    private void OnSend1PropertiesChanged(Properties? properties)
    {
        Send1Trim = CalcSendTrimFromProperties(properties);
    }

    private void OnSend2PropertiesChanged(Properties? properties)
    {
        Send2Trim = CalcSendTrimFromProperties(properties);
    }

    private void OnSend3PropertiesChanged(Properties? properties)
    {
        Send3Trim = CalcSendTrimFromProperties(properties);
    }

    private void OnSend4PropertiesChanged(Properties? properties)
    {
        Send4Trim = CalcSendTrimFromProperties(properties);
    }

    private static float CalcSendTrimFromProperties(Properties? properties)
    {
        if (properties is null) return 0.0f;

        var volumes =
            Audio.Pan.AttenuateFromExternal(
                properties.Channels.Select(c => (double)c.Volume)
                    .ToArray());

        if (volumes.Length < 2)
        {
            return 0.0f;
        }
        else
        {
            var (pan, volume) =
                Audio.Pan.GetPanAndVolumeFromGains(volumes[0], volumes[1]);

            return (float)volume;
        }
    }

    private void SetVolumesForSend(int index, double volume)
    {
        if (_sendLoopbacks.Count > index)
            _sendLoopbacks[index].PlaybackNode.SetVolumes(
                Audio.Pan.BoostToExternal(
                    Audio.Pan.GetGainsFromPanAndVolume(0.0, volume)));
    }

    public FilterChain? FilterChain
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ChannelStrip ChannelStrip { get; }

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

    public void DeleteAction()
    {
    }

    public ObservableCollection<IRoutingTarget>
        AudioFromRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioFromRoutingTarget
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send1Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send2Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send3Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send4Trim
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasFilter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public async Task AddFilterAction()
    {
        var dialogViewModel = new AddFilterChainViewModel(_appDataService);
        var dialog = new AddFilterChainView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel is
            { DialogResult: true, SelectedFilterGraph: not null })
        {
            var channelStrip = await _mixerService.AddFilterToChannelStrip(
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

    public void Dispose()
    {
        _disposables.Dispose();
        if (PanAndVolume is PanAndVolumeViewModel panAndVolume)
            panAndVolume.Dispose();

        _sendLoopbacks[0].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;

        _sendLoopbacks[1].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;

        _sendLoopbacks[2].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;

        _sendLoopbacks[3].PlaybackNode.PropertiesChanged -=
            OnSend1PropertiesChanged;

        GC.SuppressFinalize(this);
    }
}