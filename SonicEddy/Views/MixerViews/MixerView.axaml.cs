using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.MixerViewModels;

namespace SonicEddy.Views.MixerViews;

public partial class MixerView : ReactiveUserControl<MixerViewModel>
{
    public MixerView()
    {
        InitializeComponent();
    }
}