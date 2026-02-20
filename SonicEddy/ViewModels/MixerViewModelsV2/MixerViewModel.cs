using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.ViewModels.CustomControlTesterViewModels;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MixerViewModel : ViewModelBase, IRoutableViewModel,
    IActivatableViewModel, IDisposable
{
    public ICommand SelectChannelCommand { get; }

    public MixerViewModel(string? urlPathSegment, IScreen hostScreen)
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
    }

    public ObservableCollection<IChannelStrip>? ChannelStrips { get; set; }
    public ObservableCollection<IInputChannel>? InputChannels { get; set; }
    public ObservableCollection<IOutputChannel>? OutputChannels { get; set; }
    public ObservableCollection<IReturnChannel>? ReturnChannels { get; set; }

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