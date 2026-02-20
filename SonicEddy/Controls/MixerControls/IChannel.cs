using System.Collections.Generic;
using System.Windows.Input;

namespace SonicEddy.Controls.MixerControls;

public interface IChannel
{
    string Text { get; }
    bool IsSelected { get; set; }
    List<ParameterCollection>? Parameters { get; }
    IPanAndVolume PanAndVolume { get; }
    ICommand SelectChannelCommand { get; set; }
    
    void OnSelectChannel();
}