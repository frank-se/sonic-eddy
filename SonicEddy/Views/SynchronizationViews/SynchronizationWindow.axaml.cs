using System;
using Avalonia.Controls;
using SonicEddy.Tools;

namespace SonicEddy.Views.SynchronizationViews;

public partial class SynchronizationWindow : Window
{
    public SynchronizationWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        Closed -= OnClosed;
    }
}
