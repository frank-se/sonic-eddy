using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.MixerOverviewViews;

public partial class MixerOverviewWindow : Window
{
    public MixerOverviewWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-overview");
    }
}
