using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.GamepadSetupViews;

public partial class GamepadSetupWindow : Window
{
    public GamepadSetupWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
