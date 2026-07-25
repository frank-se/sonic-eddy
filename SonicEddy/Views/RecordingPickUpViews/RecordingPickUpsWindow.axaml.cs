using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.RecordingPickUpViews;

public partial class RecordingPickUpsWindow : Window
{
    public RecordingPickUpsWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
