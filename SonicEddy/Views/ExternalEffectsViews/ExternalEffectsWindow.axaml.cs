using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.ExternalEffectsViews;

public partial class ExternalEffectsWindow : Window
{
    public ExternalEffectsWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}
