using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.VirtualOutputsViews;

public partial class VirtualOutputsWindow : Window
{
    public VirtualOutputsWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
