using System;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.Controls.GraphEditorControl;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;

namespace SonicEddy.Views.FilterGraphBuilderViews;

public partial class
    FilterGraphBuilderView : ReactiveUserControl<FilterGraphBuilderViewModel>
{
    public FilterGraphBuilderView()
    {
        InitializeComponent();
    }
}