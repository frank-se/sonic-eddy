using Avalonia.Controls;
using Avalonia.ReactiveUI;
using SonicEddy.ViewModels.ObjectBrowserViewModels;

namespace SonicEddy.Views.ObjectBrowserViews;

public partial class
    ObjectBrowserView : ReactiveUserControl<ObjectBrowserViewModel>
{
    public ObjectBrowserView()
    {
        InitializeComponent();
    }
}