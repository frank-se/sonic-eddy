using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.StreamingControlViews;

public partial class StreamingControlWindow : Window
{
    public StreamingControlWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
