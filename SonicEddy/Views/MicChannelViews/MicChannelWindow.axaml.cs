using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.MicChannelViews;

public partial class MicChannelWindow : Window
{
    public MicChannelWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-mic-channel");
    }
}
