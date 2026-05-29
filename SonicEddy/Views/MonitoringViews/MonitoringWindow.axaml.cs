using System;
using Avalonia.Controls;

namespace SonicEddy.Views.MonitoringViews;

public partial class MonitoringWindow : Window
{
    public MonitoringWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        Closed -= OnClosed;
    }
}
