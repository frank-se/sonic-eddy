using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.MidiSyncViews;

public partial class MidiSyncWindow : Window
{
    public MidiSyncWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
