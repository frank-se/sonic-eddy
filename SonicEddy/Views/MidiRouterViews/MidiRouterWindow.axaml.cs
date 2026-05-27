using System;
using Avalonia.Controls;

namespace SonicEddy.Views.MidiRouterViews;

public partial class MidiRouterWindow : Window
{
    public MidiRouterWindow()
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
