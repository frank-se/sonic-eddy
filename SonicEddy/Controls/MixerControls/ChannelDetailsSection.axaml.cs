using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Controls.MixerControls;

public partial class ChannelDetailsSection : UserControl
{
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

    public ChannelDetailsSection()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ParameterCollectionsProperty)
        {
            SetupTabEnabled();
        }
        else if (change.Property == SelectedChannelProperty)
        {
            if (change.NewValue is not IChannel channel) return;
            UpdateDetails(channel);
        }
    }

    private readonly List<Control> _detailsLeftControls = [];
    private readonly List<Control> _detailsRightControls = [];

    private void UpdateDetails(IChannel channel)
    {
        foreach (var control in _detailsLeftControls)
        {
            PART_DetailsLeftColumn.Children.Remove(control);
        }

        _detailsLeftControls.Clear();

        foreach (var control in _detailsRightControls)
        {
            PART_DetailsRightColumn.Children.Remove(control);
        }

        _detailsRightControls.Clear();

        var name = new TextBlock()
        {
            Text = channel.Text
        };

        AddDetailsLeftControl(name);

        if (channel is IChannelStrip channelStrip)
        {
            var cs = channelStrip.ChannelStrip;

            AddDetailsLeftControl(
                new TextBlock()
                {
                    Text = "Channel Strip",
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(6)
                });

            AddLoopbackModuleDetails("Input Module", cs.InputLoopback,
                AddDetailsLeftControl);

            if (channelStrip.HasFilter &&
                channelStrip is ChannelStripViewModel viewModel)
            {
                AddFilterChainModuleDetails("Filter Chain",
                    viewModel.FilterChain, AddDetailsLeftControl);
            }

            AddLoopbackModuleDetails("Output Module", cs.OutputLoopback,
                AddDetailsLeftControl);

            AddDetailsRightControl(
                new TextBlock()
                {
                    Text = "Sends",
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(6)
                });

            foreach (var send in cs.SendLoopbacks)
            {
                AddLoopbackModuleDetails($"Send Module {send.Name}", send,
                    AddDetailsRightControl);
            }
        }
    }

    private void AddFilterChainModuleDetails(string name, FilterChain module,
        Action<Control> AddAction)
    {
        AddAction(
            new TextBlock()
            {
                Text = name,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(6)
            });

        AddAction(
            new TextBlock()
            {
                Text = "Capture Node",
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(4)
            });

        AddAction(
            new TextBlock()
            {
                Text = $"Object Serial: {module.CaptureNode.ObjectSerial}"
            });

        AddAction(
            new TextBlock()
            {
                Text = $"Object Id: {module.CaptureNode.ObjectId}"
            });

        // ReSharper disable once MergeIntoPattern
        if (module.CaptureNode.Properties.IsCompleted &&
            module.CaptureNode.Properties.Result is not null)
        {
            AddAction(
                new TextBlock()
                {
                    Text =
                        $"Volumes: {string.Join(", ", module.CaptureNode.Properties.Result.Channels.Select(c => c.Volume))}"
                });
        }

        AddAction(
            new TextBlock()
            {
                Text = "Playback Node",
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(4)
            });

        AddAction(
            new TextBlock()
            {
                Text =
                    $"Object Serial: {module.PlaybackNode.ObjectSerial}"
            });

        AddAction(
            new TextBlock()
            {
                Text = $"Object Id: {module.PlaybackNode.ObjectId}"
            });

        // ReSharper disable once MergeIntoPattern
        if (module.PlaybackNode.Properties.IsCompleted &&
            module.PlaybackNode.Properties.Result is not null)
        {
            AddAction(
                new TextBlock()
                {
                    Text =
                        $"Volumes: {string.Join(", ", module.PlaybackNode.Properties.Result.Channels.Select(c => c.Volume))}"
                });
        }
    }

    private void AddLoopbackModuleDetails(string name, LoopbackModule module,
        Action<Control> AddAction)
    {
        AddAction(
            new TextBlock()
            {
                Text = name,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(6)
            });

        AddAction(
            new TextBlock()
            {
                Text = "Capture Node",
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(4)
            });

        AddAction(
            new TextBlock()
            {
                Text = $"Object Serial: {module.CaptureNode.ObjectSerial}"
            });

        AddAction(
            new TextBlock()
            {
                Text = $"Object Id: {module.CaptureNode.ObjectId}"
            });

        // ReSharper disable once MergeIntoPattern
        if (module.CaptureNode.Properties.IsCompleted &&
            module.CaptureNode.Properties.Result is not null)
        {
            AddAction(
                new TextBlock()
                {
                    Text =
                        $"Volumes: {string.Join(", ", module.CaptureNode.Properties.Result.Channels.Select(c => c.Volume))}"
                });
        }

        AddAction(
            new TextBlock()
            {
                Text = "Playback Node",
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(4)
            });

        AddAction(
            new TextBlock()
            {
                Text =
                    $"Object Serial: {module.PlaybackNode.ObjectSerial}"
            });

        AddAction(
            new TextBlock()
            {
                Text = $"Object Id: {module.PlaybackNode.ObjectId}"
            });

        // ReSharper disable once MergeIntoPattern
        if (module.PlaybackNode.Properties.IsCompleted &&
            module.PlaybackNode.Properties.Result is not null)
        {
            AddAction(
                new TextBlock()
                {
                    Text =
                        $"Volumes: {string.Join(", ", module.PlaybackNode.Properties.Result.Channels.Select(c => c.Volume))}"
                });
        }
    }

    private void AddDetailsLeftControl(Control control)
    {
        _detailsLeftControls.Add(control);
        PART_DetailsLeftColumn.Children.Add(control);
    }

    private void AddDetailsRightControl(Control control)
    {
        _detailsRightControls.Add(control);
        PART_DetailsRightColumn.Children.Add(control);
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