using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SonicEddy.ViewModels.MidiConnectionEditorViewModels;

namespace SonicEddy.Views.MidiConnectionEditorViews;

public partial class
    MidiConnectionEditorView : ReactiveUserControl<
    MidiConnectionEditorViewModel>
{
    public MidiConnectionEditorView()
    {
        InitializeComponent();
    }
}