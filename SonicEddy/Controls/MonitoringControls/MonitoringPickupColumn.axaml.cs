using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using SonicEddy.Services.Monitoring;
using Splat;

namespace SonicEddy.Controls.MonitoringControls;

public partial class MonitoringPickupColumn : UserControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<MonitoringPickupColumn, string>(
            nameof(Header), defaultValue: string.Empty);

    public static readonly StyledProperty<int> LayerIndexProperty =
        AvaloniaProperty.Register<MonitoringPickupColumn, int>(nameof(LayerIndex));

    public static readonly StyledProperty<MonitoringChannelType> ChannelTypeProperty =
        AvaloniaProperty.Register<MonitoringPickupColumn, MonitoringChannelType>(
            nameof(ChannelType));

    public static readonly StyledProperty<int> ChannelIndexProperty =
        AvaloniaProperty.Register<MonitoringPickupColumn, int>(nameof(ChannelIndex));

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public int LayerIndex
    {
        get => GetValue(LayerIndexProperty);
        set => SetValue(LayerIndexProperty, value);
    }

    public MonitoringChannelType ChannelType
    {
        get => GetValue(ChannelTypeProperty);
        set => SetValue(ChannelTypeProperty, value);
    }

    public int ChannelIndex
    {
        get => GetValue(ChannelIndexProperty);
        set => SetValue(ChannelIndexProperty, value);
    }

    private readonly IMonitoringLinkService? _service;
    private bool _updatingSource;
    private bool _updatingFader;
    private bool _restoringState;

    public MonitoringPickupColumn()
    {
        InitializeComponent();

        _service = Locator.Current.GetService<IMonitoringLinkService>();

        PreButton.IsCheckedChanged += (_, _) => OnSourceCheckedChanged(PreButton);
        PostButton.IsCheckedChanged += (_, _) => OnSourceCheckedChanged(PostButton);
        OutButton.IsCheckedChanged += (_, _) => OnSourceCheckedChanged(OutButton);

        PreFaderButton.Click += (_, _) => SetFader(PreFaderButton);
        PostFaderButton.Click += (_, _) => SetFader(PostFaderButton);

        SetFader(PostFaderButton);

        Loaded += (_, _) => RestoreState();
    }

    private void OnSourceCheckedChanged(ToggleButton sender)
    {
        if (_updatingSource) return;

        if (sender.IsChecked == true)
        {
            _updatingSource = true;
            PreButton.IsChecked = sender == PreButton;
            PostButton.IsChecked = sender == PostButton;
            OutButton.IsChecked = sender == OutButton;
            _updatingSource = false;
        }

        NotifyService();
    }

    private void SetFader(ToggleButton selected)
    {
        if (_updatingFader) return;
        _updatingFader = true;
        PreFaderButton.IsChecked = selected == PreFaderButton;
        PostFaderButton.IsChecked = selected == PostFaderButton;
        _updatingFader = false;

        if (OutButton.IsChecked == true)
            NotifyService();
    }

    private void NotifyService()
    {
        if (_restoringState || _service is null) return;
        var key = new MonitoringChannelKey(LayerIndex, ChannelType, ChannelIndex);
        _service.SetSource(key, DeriveSource());
    }

    private MonitoringSource DeriveSource()
    {
        if (PreButton.IsChecked == true) return MonitoringSource.Pre;
        if (PostButton.IsChecked == true) return MonitoringSource.Post;
        if (OutButton.IsChecked == true)
            return PreFaderButton.IsChecked == true
                ? MonitoringSource.OutPreFader
                : MonitoringSource.OutPostFader;
        return MonitoringSource.None;
    }

    private void RestoreState()
    {
        if (_service is null) return;
        var key = new MonitoringChannelKey(LayerIndex, ChannelType, ChannelIndex);
        ApplySource(_service.GetSource(key));
    }

    private void ApplySource(MonitoringSource source)
    {
        _restoringState = true;
        _updatingSource = true;
        PreButton.IsChecked = source == MonitoringSource.Pre;
        PostButton.IsChecked = source == MonitoringSource.Post;
        OutButton.IsChecked = source is MonitoringSource.OutPreFader or MonitoringSource.OutPostFader;
        _updatingSource = false;

        if (source == MonitoringSource.OutPreFader)
            SetFader(PreFaderButton);
        else if (source == MonitoringSource.OutPostFader)
            SetFader(PostFaderButton);

        _restoringState = false;
    }
}
