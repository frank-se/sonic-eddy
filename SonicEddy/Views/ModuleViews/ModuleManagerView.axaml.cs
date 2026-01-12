using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.ModuleViewModels;

namespace SonicEddy.Views.ModuleViews;

public partial class
    ModuleManagerView : ReactiveUserControl<ModuleManagerViewModel>
{
    public ModuleManagerView()
    {
        InitializeComponent();
    }
}