using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.GlobalMasterViews;

public partial class GlobalMasterWindow : Window
{
    public GlobalMasterWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-global-master");
    }
}
