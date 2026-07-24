using System;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using SonicEddy.Tools;
using Splat;

namespace SonicEddy.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var logger = Locator.Current.GetService<ILoggerFactory>()
            ?.CreateLogger("SonicEddy.Tools.WaylandAppId");
        WaylandAppId.TrySet(this, "sonic-eddy", logger);
    }
}