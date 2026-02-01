using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.MixerManagerViewModels;

namespace SonicEddy.Views.MixerManagerViews;

public partial class MixerManagerView : ReactiveUserControl<MixerManagerViewModel>
{
    public MixerManagerView()
    {
        InitializeComponent();
    }
}