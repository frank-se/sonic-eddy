using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Views.MixerViewsV2;

public partial class MixerView : ReactiveUserControl<MixerViewModel>
{
    public MixerView()
    {
        InitializeComponent();
    }
}