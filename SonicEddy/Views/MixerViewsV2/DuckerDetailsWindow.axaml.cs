using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.MixerViewsV2;

public partial class DuckerDetailsWindow : Window
{
    public DuckerDetailsWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
