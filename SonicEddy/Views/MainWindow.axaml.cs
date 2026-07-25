using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-main-mixer");
    }
}