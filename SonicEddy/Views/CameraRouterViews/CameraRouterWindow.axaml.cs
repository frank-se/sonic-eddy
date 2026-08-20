using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.CameraRouterViews;

public partial class CameraRouterWindow : Window
{
    public CameraRouterWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
