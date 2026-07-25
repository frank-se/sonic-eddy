using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SonicEddy.Tools;

namespace SonicEddy.Views.ModuleManagerViews;

public partial class ModuleManagerWindow : Window
{
    public ModuleManagerWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}