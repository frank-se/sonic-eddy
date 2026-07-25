using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.ClickSyncViews;

public partial class ClickSyncWindow : Window
{
    public ClickSyncWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
