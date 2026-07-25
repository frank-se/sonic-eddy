using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SonicEddy.Tools;

namespace SonicEddy.Views.FilterGraphManagerViews;

public partial class FilterGraphWindow : Window
{
    public FilterGraphWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}