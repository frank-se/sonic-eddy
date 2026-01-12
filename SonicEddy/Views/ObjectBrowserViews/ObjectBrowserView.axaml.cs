using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
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