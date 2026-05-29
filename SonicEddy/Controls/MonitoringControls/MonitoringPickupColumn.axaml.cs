using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace SonicEddy.Controls.MonitoringControls;

public partial class MonitoringPickupColumn : UserControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<MonitoringPickupColumn, string>(
            nameof(Header), defaultValue: string.Empty);

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public MonitoringPickupColumn()
    {
        InitializeComponent();

        PreButton.IsCheckedChanged += (_, _) => OnSourceCheckedChanged(PreButton);
        PostButton.IsCheckedChanged += (_, _) => OnSourceCheckedChanged(PostButton);
        OutButton.IsCheckedChanged += (_, _) => OnSourceCheckedChanged(OutButton);

        PreFaderButton.Click += (_, _) => SetFader(PreFaderButton);
        PostFaderButton.Click += (_, _) => SetFader(PostFaderButton);

        SetFader(PostFaderButton);
    }

    private bool _updatingSource;
    private void OnSourceCheckedChanged(ToggleButton sender)
    {
        if (_updatingSource || sender.IsChecked != true) return;
        _updatingSource = true;
        PreButton.IsChecked = sender == PreButton;
        PostButton.IsChecked = sender == PostButton;
        OutButton.IsChecked = sender == OutButton;
        _updatingSource = false;
    }

    private bool _updatingFader;
    private void SetFader(ToggleButton selected)
    {
        if (_updatingFader) return;
        _updatingFader = true;
        PreFaderButton.IsChecked = selected == PreFaderButton;
        PostFaderButton.IsChecked = selected == PostFaderButton;
        _updatingFader = false;
    }
}
