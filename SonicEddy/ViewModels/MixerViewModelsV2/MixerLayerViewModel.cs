using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Windows.Input;
using DynamicData;
using Fr.Pw.Midi.PInvoke;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerViewModels;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MixerLayerViewModel : ViewModelBase,
    IActivatableViewModel, IDisposable
{
    public ICommand SelectChannelCommand { get; }

    private readonly IMixerService _mixerService;
    private readonly IMixerViewModelService _mixerViewModelService;
    private readonly IMidiControllerService _midiControllerService;
    private readonly CompositeDisposable _disposables = new();
    private readonly ILogger<MixerLayerViewModel> _logger;

    public MixerLayerViewModel(
        IMixerService mixerService,
        IMixerViewModelService mixerViewModelService,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        ObservableCollection<IRoutingTarget> audioToRoutingTargetsChannelStrips,
        ObservableCollection<IRoutingTarget> audioToRoutingTargetsGroupChannels,
        ObservableCollection<IRoutingTarget> audioToRoutingTargetsMasterChannel,
        IMidiControllerService midiControllerService, ulong layerId,
        ILogger<MixerLayerViewModel> logger)
    {
        var internalChange = false;

        ICommand selectChannelCommand =
            ReactiveCommand.Create<IChannel>(channel =>
            {
                if (channel.IsSelected)
                {
                    Console.WriteLine("Already selected, ignore");

                    if (SelectedChannel is null)
                    {
                        throw new ArgumentException(
                            "Channel is selected, but selected channel is null!");
                    }

                    return;
                }

                internalChange = true;
                ClearSelectedChannel();
                SetSelectedChannel(channel);
                internalChange = false;
            });

        SelectChannelCommand = selectChannelCommand;

        _mixerService = mixerService;
        _mixerViewModelService = mixerViewModelService;
        AudioFromRoutingTargets = audioFromRoutingTargets;
        AudioToRoutingTargetsChannelStrips = audioToRoutingTargetsChannelStrips;
        AudioToRoutingTargetsGroupChannels = audioToRoutingTargetsGroupChannels;
        AudioToRoutingTargetsMasterChannel = audioToRoutingTargetsMasterChannel;
        _midiControllerService = midiControllerService;
        _logger = logger;

        this.WhenAnyValue(x => x.SelectedChannel).Subscribe(selectedChannel =>
        {
            if (!internalChange) return;

            switch (selectedChannel)
            {
                case IChannelStrip channelStrip:
                    _midiControllerService.SetSelectedChannel(
                        ChannelType.Channel, channelStrip.ChannelId);
                    break;
                case IGroupChannel groupChannel:
                    _midiControllerService.SetSelectedChannel(
                        ChannelType.GroupChannel, groupChannel.ChannelId);
                    break;
                default:
                    _midiControllerService.ClearSelectedChannel();
                    break;
            }
        }).DisposeWith(_disposables);
    }

    public void ClearSelectedChannel()
    {
        SelectedChannel?.IsSelected = false;
        SelectedChannel = null;
        SelectedChannelParameters = [];
    }

    public void SetSelectedChannel(IChannel channel)
    {
        channel.IsSelected = true;
        SelectedChannel = channel;
        SelectedChannelParameters = channel.Parameters ?? [];
    }

    public void SetSelectedChannel(int channelId)
    {
        if (ChannelStrips is null)
        {
            _logger.LogError("Channel Strips list is null");

            return;
        }

        if (channelId >= (ChannelStrips?.Count ?? 0))
        {
            _logger.LogError("Channel {ChannelId} out of bounds", channelId);

            return;
        }

        var channel = ChannelStrips![channelId];
        SetSelectedChannel(channel);
    }

    public void SetSelectedGroupChannel(int channelId)
    {
        if (GroupChannels is null)
        {
            _logger.LogError("Group channels list is null");

            return;
        }

        if (channelId >= (GroupChannels?.Count ?? 0))
        {
            _logger.LogError("Group channel {ChannelId} out of bounds",
                channelId);

            return;
        }

        var channel = GroupChannels![channelId];
        SetSelectedChannel(channel);
    }

    public int NumberOfRows { get; } = 6;

    public int NumberOfColumns { get; } = 4;

    public void ActivateNextPluginPage()
    {
        _logger.LogTrace("ActivateNextPluginPage");

        if (SelectedChannel is null)
        {
            _logger.LogTrace(
                "SelectedChannel is null, ignoring previous plugin page navigation");
            return;
        }

        if (PluginDetailsSelectedPage is null)
        {
            _logger.LogTrace("No current plugin page selected, ignoring.");
            return;
        }

        var currentPluginName = PluginDetailsSelectedPage.Name;
        var selectedPlugin =
            SelectedChannel.Parameters?.FirstOrDefault(p =>
                p.Name == currentPluginName);

        if (selectedPlugin is null)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Plugin {Name} could not be found",
                    currentPluginName);
            return;
        }

        var numberOfPagesForCurrentPlugin =
            selectedPlugin.Parameters.Count / (NumberOfColumns * NumberOfRows);

        var nextPageNumber = PluginDetailsSelectedPage.PageNumber + 1;
        if (nextPageNumber < numberOfPagesForCurrentPlugin)
        {
            PluginDetailsSelectedPage = PluginDetailsSelectedPage with
            {
                PageNumber = nextPageNumber
            };

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Set selected page number {PageNumber} for plugin {PluginName}",
                    nextPageNumber, currentPluginName);

            return;
        }

        var currentPluginIndex =
            SelectedChannel.Parameters?.IndexOf(selectedPlugin);

        if (currentPluginIndex is null)
        {
            _logger.LogError("Couldn't find plugin index");
            return;
        }

        var nextPluginIndex = currentPluginIndex + 1;

        if (nextPluginIndex >= SelectedChannel?.Parameters?.Count)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace(
                    "Next plugin index {NextPluginIndex} would be past the end, ignoring",
                    nextPluginIndex);

            return;
        }

        var nextSelectedPlugin =
            SelectedChannel!.Parameters![nextPluginIndex.Value];

        PluginDetailsSelectedPage = new(nextSelectedPlugin.Name, 0);
    }

    public void ActivatePreviousPluginPage()
    {
        _logger.LogTrace("ActivatePreviousPluginPage");

        if (SelectedChannel is null)
        {
            _logger.LogTrace(
                "SelectedChannel is null, ignoring previous plugin page navigation");
            return;
        }

        if (PluginDetailsSelectedPage is null)
        {
            _logger.LogTrace("No current plugin page selected, ignoring.");
            return;
        }

        var currentPluginName = PluginDetailsSelectedPage.Name;
        var selectedPlugin =
            SelectedChannel.Parameters?.FirstOrDefault(p =>
                p.Name == currentPluginName);

        if (selectedPlugin is null)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Plugin {Name} could not be found",
                    currentPluginName);
            return;
        }

        if (PluginDetailsSelectedPage.PageNumber > 0)
        {
            var nextPageNumber = PluginDetailsSelectedPage.PageNumber - 1;
            PluginDetailsSelectedPage = PluginDetailsSelectedPage with
            {
                PageNumber = nextPageNumber
            };

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Set selected page number {PageNumber} for plugin {PluginName}",
                    nextPageNumber, currentPluginName);

            return;
        }

        var currentPluginIndex =
            SelectedChannel.Parameters?.IndexOf(selectedPlugin);

        if (currentPluginIndex is null)
        {
            _logger.LogError("Couldn't find plugin index");
            return;
        }

        if (currentPluginIndex == 0)
        {
            _logger.LogError("Already leftmost page, ignoring");
            return;
        }

        var nextPluginIndex = currentPluginIndex - 1;
        var nextPlugin = SelectedChannel!.Parameters![nextPluginIndex.Value];

        PluginDetailsSelectedPage = new(nextPlugin.Name, 0);
    }

    public void SetupEvents()
    {
        _mixerService.InputsChanged += OnInputsChanged;
        _mixerService.OutputsChanged += OnOutputsChanged;
    }

    private void OnInputsChanged(List<InputChannel> inputChannels)
    {
        if (InputChannels is null) return;

        DeleteRemovedInputChannels(inputChannels);
        AddNewInputChannels(inputChannels);
    }

    private void DeleteRemovedInputChannels(List<InputChannel> inputChannels)
    {
        if (InputChannels is null) return;

        var newObjectSerials =
            inputChannels.Select(c => c.PlaybackNode.ObjectSerial).ToArray();

        var deletedChannels = InputChannels.Where(c =>
            !newObjectSerials.Contains(c.PlaybackNodeObjectSerial));

        foreach (var deletedChannel in deletedChannels)
        {
            InputChannels.Remove(deletedChannel);

            var audioFromRoutingTarget =
                AudioFromRoutingTargets.FirstOrDefault(c =>
                {
                    if (c.Channel is not IInputChannel inputChannel)
                        return false;

                    return inputChannel.PlaybackNodeObjectSerial ==
                           deletedChannel.PlaybackNodeObjectSerial;
                });

            if (audioFromRoutingTarget is null) continue;

            AudioFromRoutingTargets.Remove(audioFromRoutingTarget);
        }
    }

    private void AddNewInputChannels(List<InputChannel> inputChannels)
    {
        if (InputChannels is null) return;

        var currentPlaybackNodeObjectSerials =
            InputChannels.Select(i => i.PlaybackNodeObjectSerial).ToArray();

        var newChannels = inputChannels.Where(c =>
            !currentPlaybackNodeObjectSerials.Contains(
                c.PlaybackNode.ObjectSerial));

        var newViewModels = newChannels.Select(c =>
            _mixerViewModelService.ConvertInputChannel(c,
                SelectChannelCommand)).ToArray();

        InputChannels.AddRange(newViewModels);

        foreach (var channel in newViewModels)
        {
            AudioFromRoutingTargets.Add(
                new RoutingTargetViewModel(channel.Name, channel));
        }
    }

    private void OnOutputsChanged(List<OutputChannel> outputChannels)
    {
        if (OutputChannels is null) return;

        DeleteRemovedOutputChannels(outputChannels);
        AddNewOutputChannels(outputChannels);
    }

    private void DeleteRemovedOutputChannels(List<OutputChannel> outputChannels)
    {
        if (OutputChannels is null) return;

        var newObjectSerials =
            outputChannels.Select(c => c.CaptureNode.ObjectSerial).ToArray();

        var deletedChannels = OutputChannels.Where(c =>
            !newObjectSerials.Contains(c.CaptureNodeObjectSerial));

        foreach (var deletedChannel in deletedChannels)
        {
            OutputChannels.Remove(deletedChannel);
        }
    }

    private void AddNewOutputChannels(List<OutputChannel> outputChannels)
    {
        if (OutputChannels is null) return;

        var currentCaptureNodeObjectSerials =
            OutputChannels.Select(i => i.CaptureNodeObjectSerial).ToArray();

        var newChannels = outputChannels.Where(c =>
            !currentCaptureNodeObjectSerials.Contains(c.CaptureNode
                .ObjectSerial));

        var newViewModels = newChannels.Select(c =>
            _mixerViewModelService.ConvertOutputChannel(c,
                SelectChannelCommand));

        OutputChannels.AddRange(newViewModels);
    }

    public ObservableCollection<IChannelStrip>? ChannelStrips { get; set; }
    public ObservableCollection<IGroupChannel>? GroupChannels { get; set; }
    public ObservableCollection<IMasterChannel>? MasterChannels { get; set; }

    public ObservableCollection<IInputChannel>? InputChannels { get; set; }
    public ObservableCollection<IOutputChannel>? OutputChannels { get; set; }
    public ObservableCollection<IReturnChannel>? ReturnChannels { get; set; }

    public readonly ObservableCollection<IRoutingTarget>
        AudioFromRoutingTargets;

    public readonly ObservableCollection<IRoutingTarget>
        AudioToRoutingTargetsChannelStrips;

    public readonly ObservableCollection<IRoutingTarget>
        AudioToRoutingTargetsGroupChannels;

    public readonly ObservableCollection<IRoutingTarget>
        AudioToRoutingTargetsMasterChannel;

    public IChannel? SelectedChannel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<ParameterCollection> SelectedChannelParameters
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public PluginPageSelectorSelectedPage? PluginDetailsSelectedPage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ViewModelActivator Activator { get; } = new();

    public void Dispose()
    {
        Activator.Dispose();
        GC.SuppressFinalize(this);
    }
}