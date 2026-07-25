using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SonicEddy.Tools;

namespace SonicEddy.Views.PreferencesViews;

public partial class PreferencesWindow : Window
{
    public PreferencesWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}