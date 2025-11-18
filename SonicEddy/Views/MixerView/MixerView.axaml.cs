using Avalonia.ReactiveUI;
using SonicEddy.ViewModels.MixerViewModels;

namespace SonicEddy.Views.MixerView;

public partial class MixerView : ReactiveUserControl<MixerViewModel>
{
    public MixerView()
    {
        InitializeComponent();
        DetachedFromLogicalTree += (_, _) =>
            ViewModel?.StopProcessing();
    }
}