using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class GlobalReturnChannelsViewModel : ViewModelBase
{
    public GlobalReturnChannelsViewModel(ReturnChannelViewModel return1,
        ReturnChannelViewModel return2)
    {
        Return1 = return1;
        Return2 = return2;

        ICommand selectChannelCommand =
            ReactiveCommand.Create<IChannel>(channel =>
            {
                if (channel.IsSelected) return;
                ClearSelectedChannel();
                SetSelectedChannel(channel);
            });

        Return1.SelectChannelCommand = selectChannelCommand;
        Return2.SelectChannelCommand = selectChannelCommand;
    }

    public ReturnChannelViewModel Return1 { get; }
    public ReturnChannelViewModel Return2 { get; }

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
