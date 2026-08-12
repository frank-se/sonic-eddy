using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class MicChannelsViewModel : ViewModelBase
{
    public MicChannelsViewModel(MicChannelViewModel mic1,
        MicChannelViewModel mic2)
    {
        Mic1 = mic1;
        Mic2 = mic2;

        ICommand selectChannelCommand =
            ReactiveCommand.Create<IChannel>(channel =>
            {
                if (channel.IsSelected) return;
                ClearSelectedChannel();
                SetSelectedChannel(channel);
            });

        Mic1.SelectChannelCommand = selectChannelCommand;
        Mic2.SelectChannelCommand = selectChannelCommand;
    }

    public MicChannelViewModel Mic1 { get; }
    public MicChannelViewModel Mic2 { get; }

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

    private void ClearSelectedChannel()
    {
        SelectedChannel?.IsSelected = false;
        SelectedChannel = null;
        SelectedChannelParameters = [];
    }

    private void SetSelectedChannel(IChannel channel)
    {
        channel.IsSelected = true;
        SelectedChannel = channel;
        SelectedChannelParameters = channel.Parameters ?? [];
    }
}
