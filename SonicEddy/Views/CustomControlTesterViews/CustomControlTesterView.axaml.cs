using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.CustomControlTesterViewModels;

namespace SonicEddy.Views.CustomControlTesterViews;

public partial class
    CustomControlTesterView : ReactiveUserControl<CustomControlTesterViewModel>
{
    public CustomControlTesterView()
    {
        InitializeComponent();
    }
}