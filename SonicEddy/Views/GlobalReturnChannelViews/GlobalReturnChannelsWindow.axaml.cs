using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.GlobalReturnChannelViews;

public partial class GlobalReturnChannelsWindow : Window
{
    public GlobalReturnChannelsWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-global-return-channels");
    }
}
