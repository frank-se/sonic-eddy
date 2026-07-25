using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.VirtualInputsViews;

public partial class VirtualInputsWindow : Window
{
    public VirtualInputsWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}