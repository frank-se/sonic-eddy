using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.DrumMixerViews;

public partial class DrumMixerWindow : Window
{
    public DrumMixerWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-drum-mixer");
    }
}
