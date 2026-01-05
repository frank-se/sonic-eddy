using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using SonicEddy.ViewModels.ProAudioStreamsViewModels;

namespace SonicEddy.Views.ProAudioStreamsViews;

public partial class
    ProAudioStreamsView : ReactiveUserControl<ProAudioStreamsViewModel>
{
    public ProAudioStreamsView()
    {
        InitializeComponent();
    }
}