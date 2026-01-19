using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.FilterGraphManagerViewModels;

namespace SonicEddy.Views.FilterGraphManagerViews;

public partial class
    FilterGraphManagerView : ReactiveUserControl<FilterGraphManagerViewModel>
{
    public FilterGraphManagerView()
    {
        InitializeComponent();
    }
}