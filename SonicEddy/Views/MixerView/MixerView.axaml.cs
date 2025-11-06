using Avalonia.ReactiveUI;
using SonicEddy.ViewModels.MixerViewModel;

namespace SonicEddy.Views.MixerView;

public partial class MixerView : ReactiveUserControl<MixerViewModel>
{
    public MixerView()
    {
        InitializeComponent();
    }
}