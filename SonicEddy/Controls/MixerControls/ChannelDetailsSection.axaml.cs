using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SonicEddy.Controls.MixerControls;

public partial class ChannelDetailsSection : UserControl
{
    public ChannelDetailsSection()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<IChannel?> SelectedChannelProperty =
        AvaloniaProperty.Register<ChannelDetailsSection, IChannel?>(
            nameof(SelectedChannel));

    public IChannel? SelectedChannel
    {
        get => GetValue(SelectedChannelProperty);
        set => SetValue(SelectedChannelProperty, value);
    }

    public static readonly
        StyledProperty<List<ParameterCollection>?>
        ParameterCollectionsProperty =
            AvaloniaProperty
                .Register<ChannelDetailsSection,
                    List<ParameterCollection>?>(
                    nameof(ParameterCollections));

    public List<ParameterCollection>? ParameterCollections
    {
        get => GetValue(ParameterCollectionsProperty);
        set => SetValue(ParameterCollectionsProperty, value);
    }

    public static readonly StyledProperty<PluginPageSelectorSelectedPage?>
        SelectedPageProperty =
            AvaloniaProperty
                .Register<ChannelDetailsSection, PluginPageSelectorSelectedPage
                    ?>(nameof(SelectedPage),
                    defaultValue: null,
                    defaultBindingMode: BindingMode.TwoWay);

    public PluginPageSelectorSelectedPage? SelectedPage
    {
        get => GetValue(SelectedPageProperty);
        set => SetValue(SelectedPageProperty, value);
    }

    public static readonly StyledProperty<int> NumberOfColumnsProperty =
        AvaloniaProperty.Register<ChannelDetailsSection, int>(
            nameof(NumberOfColumns),
            defaultValue: 4);

    public int NumberOfColumns
    {
        get => GetValue(NumberOfColumnsProperty);
        set => SetValue(NumberOfColumnsProperty, value);
    }

    public static readonly StyledProperty<int> NumberOfRowsProperty =
        AvaloniaProperty.Register<ChannelDetailsSection, int>(
            nameof(NumberOfColumns),
            defaultValue: 4);

    public int NumberOfRows
    {
        get => GetValue(NumberOfRowsProperty);
        set => SetValue(NumberOfRowsProperty, value);
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ParameterCollectionsProperty)
            SetupTabEnabled();
    }

    private void SetupTabEnabled()
    {
        if (PART_TabControl.Items[0] is not TabItem item) return;
        if (ParameterCollections?.Any() ?? false)
        {
            item.IsEnabled = true;
            PART_TabControl.SelectedIndex = 0;
        }
        else
        {
            item.IsEnabled = false;
            PART_TabControl.SelectedIndex = 1;
        }
    }
}