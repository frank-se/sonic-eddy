using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerViewModels;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MixerViewModel : ViewModelBase, IRoutableViewModel,
    IActivatableViewModel, IDisposable
{
    public ICommand SelectChannelCommand { get; }

    private readonly IMixerService _mixerService;
    private readonly IMixerViewModelService _mixerViewModelService;

    public MixerViewModel(string? urlPathSegment, IScreen hostScreen,
        IMixerService mixerService,
        IMixerViewModelService mixerViewModelService,
        ObservableCollection<IRoutingTarget> audioFromRoutingTargets,
        ObservableCollection<IRoutingTarget> audioToRoutingTargetsChannelStrips,
        ObservableCollection<IRoutingTarget> audioToRoutingTargetsGroupChannels,
        ObservableCollection<IRoutingTarget> audioToRoutingTargetsMasterChannel)
    {
        HostScreen = hostScreen;
        UrlPathSegment = urlPathSegment;

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

                channel.IsSelected = true;
                SelectedChannel?.IsSelected = false;
                SelectedChannel = channel;
                SelectedChannelParameters = channel.Parameters ?? [];
            });

        SelectChannelCommand = selectChannelCommand;

        _mixerService = mixerService;
        _mixerViewModelService = mixerViewModelService;
        AudioFromRoutingTargets = audioFromRoutingTargets;
        AudioToRoutingTargetsChannelStrips = audioToRoutingTargetsChannelStrips;
        AudioToRoutingTargetsGroupChannels = audioToRoutingTargetsGroupChannels;
        AudioToRoutingTargetsMasterChannel = audioToRoutingTargetsMasterChannel;
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

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();

    public void Dispose()
    {
        Activator.Dispose();
        GC.SuppressFinalize(this);
    }
}