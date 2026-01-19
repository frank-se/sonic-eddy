using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.ModuleManagerViewModels;

namespace SonicEddy.Views.ModuleManagerViews;

public partial class
    ModuleManagerView : ReactiveUserControl<ModuleManagerViewModel>
{
    public ModuleManagerView()
    {
        InitializeComponent();
    }
}