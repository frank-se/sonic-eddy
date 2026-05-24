using System;
using Avalonia.Controls;

namespace SonicEddy.Views.SynchronizationViews;

public partial class SynchronizationWindow : Window
{
    public SynchronizationWindow()
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
