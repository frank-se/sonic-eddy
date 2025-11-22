using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using SonicEddy.ViewModels.MixerViewModels;

namespace SonicEddy.Views.MixerViews;

public partial class StreamView : ReactiveUserControl<StreamViewModel>
{
    public StreamView()
    {
        InitializeComponent();
    }
}