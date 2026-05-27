using System;
using Avalonia.Controls;

namespace SonicEddy.Views.MixerViewsV2;

public partial class LooperDetailsWindow : Window
{
    public LooperDetailsWindow()
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
