using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.JackInputPortsViews;

public partial class JackInputPortsWindow : Window
{
    public JackInputPortsWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
