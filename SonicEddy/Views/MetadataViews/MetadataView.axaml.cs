using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using SonicEddy.ViewModels.MetadataViewModels;

namespace SonicEddy.Views.MetadataViews;

public partial class MetadataView : ReactiveUserControl<MetadataViewModel>
{
    public MetadataView()
    {
        InitializeComponent();
    }
}