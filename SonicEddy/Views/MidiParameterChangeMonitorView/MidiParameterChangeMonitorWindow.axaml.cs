using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SonicEddy.Tools;

namespace SonicEddy.Views.MidiParameterChangeMonitorView;

public partial class MidiParameterChangeMonitorWindow : Window
{
    public MidiParameterChangeMonitorWindow()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");
    }
}