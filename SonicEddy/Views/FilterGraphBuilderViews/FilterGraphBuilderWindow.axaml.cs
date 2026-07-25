using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SonicEddy.Tools;

namespace SonicEddy.Views.FilterGraphBuilderViews;

public partial class FilterGraphBuilderWindow : Window
{
    public FilterGraphBuilderWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}