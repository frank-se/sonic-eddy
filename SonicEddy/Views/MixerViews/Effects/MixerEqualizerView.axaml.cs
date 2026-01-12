using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.MixerViewModels.Effects;

namespace SonicEddy.Views.MixerViews.Effects;

public partial class
    MixerEqualizerView : ReactiveUserControl<MixerEqualizerViewModel>
{
    public MixerEqualizerView()
    {
        InitializeComponent();
    }
}