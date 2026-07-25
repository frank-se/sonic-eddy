using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SonicEddy.Tools;

namespace SonicEddy.Views.MetadataViews;

public partial class MetadataWindow : Window
{
    public MetadataWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}