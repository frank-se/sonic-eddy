using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Controls.MixerControls;

public class FilterSectionHeader : Grid
{
    private readonly Button _presetButton;

    public FilterSectionHeader()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*, 30, 30");
        RowDefinitions = RowDefinitions.Parse("30");

        var header = new StackPanel()
        {
            Orientation = Orientation.Horizontal
        };

        var font = new FontFamily(
            "avares://SonicEddy/Assets/Fonts/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular");

        var headerIcon = new TextBlock()
        {
            Text = "",
            FontFamily = font,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        headerIcon.Bind(IsVisibleProperty,
            new Binding("IsMidiControlled")
            {
                Source = this
            });

        header.Children.Add(headerIcon);

        var headerText = new TextBlock()
        {
            FontSize = 12,
            Text = "Filter",
            Margin = new(6)
        };

        header.Children.Add(headerText);

        SetRow(header, 0);
        SetColumn(header, 0);
        SetColumnSpan(header, 3);
        Children.Add(header);

        // Preset button — column 1, visible when HasFilter
        _presetButton = new Button()
        {
            Content = "",
            FontFamily = font,
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
        };
        _presetButton.Bind(IsVisibleProperty, new Binding("HasFilter") { Source = this });
        _presetButton.Click += OnPresetButtonClick;

        SetRow(_presetButton, 0);
        SetColumn(_presetButton, 1);
        Children.Add(_presetButton);

        // Delete button — column 2, visible when HasFilter
        Button deleteButton = new()
        {
            Content = "",
            [!Button.CommandProperty] = this[!DeleteCommandProperty],
            [!Button.CommandParameterProperty] = this[!DeleteCommandParameterProperty],
            FontFamily = font,
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
            [!Button.IsVisibleProperty] = this[!HasFilterProperty]
        };
        SetRow(deleteButton, 0);
        SetColumn(deleteButton, 2);
        Children.Add(deleteButton);

        // Add button — column 2, visible when !HasFilter
        Button addButton = new()
        {
            Content = "",
            [!Button.CommandProperty] = this[!AddCommandProperty],
            [!Button.CommandParameterProperty] = this[!AddCommandParameterProperty],
            FontFamily = font,
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
        };
        addButton.Bind(Button.IsVisibleProperty,
            new Binding("HasFilter") { Source = this, Path = "!HasFilter" });

        SetRow(addButton, 0);
        SetColumn(addButton, 2);
        Children.Add(addButton);
    }

    private void OnPresetButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var saveItem = new MenuItem { Header = "Save ..." };
        saveItem.Command = SavePresetCommand;
        menu.Items.Add(saveItem);

        if (Presets?.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var preset in Presets)
            {
                var item = new MenuItem { Header = preset.Name };
                item.Command = LoadPresetCommand;
                item.CommandParameter = preset;
                menu.Items.Add(item);
            }
        }

        menu.Open(_presetButton);
    }

    public static readonly StyledProperty<bool> HasFilterProperty =
        AvaloniaProperty.Register<FilterSectionHeader, bool>(nameof(HasFilter));

    public bool HasFilter
    {
        get => GetValue(HasFilterProperty);
        set => SetValue(HasFilterProperty, value);
    }

    public static readonly StyledProperty<bool> IsMidiControlledProperty =
        AvaloniaProperty.Register<FilterSectionHeader, bool>(nameof(IsMidiControlled));

    public bool IsMidiControlled
    {
        get => GetValue(IsMidiControlledProperty);
        set => SetValue(IsMidiControlledProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<FilterSectionHeader, ICommand?>(nameof(AddCommand));

    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly StyledProperty<object?> AddCommandParameterProperty =
        AvaloniaProperty.Register<FilterSectionHeader, object?>(nameof(AddCommandParameter));

    public object? AddCommandParameter
    {
        get => GetValue(AddCommandParameterProperty);
        set => SetValue(AddCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
        AvaloniaProperty.Register<FilterSectionHeader, ICommand?>(nameof(DeleteCommand));

    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly StyledProperty<object?> DeleteCommandParameterProperty =
        AvaloniaProperty.Register<FilterSectionHeader, object?>(nameof(DeleteCommandParameter));

    public object? DeleteCommandParameter
    {
        get => GetValue(DeleteCommandParameterProperty);
        set => SetValue(DeleteCommandParameterProperty, value);
    }

    public static readonly StyledProperty<IList<FilterChainPresetViewModel>?> PresetsProperty =
        AvaloniaProperty.Register<FilterSectionHeader, IList<FilterChainPresetViewModel>?>(
            nameof(Presets));

    public IList<FilterChainPresetViewModel>? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SavePresetCommandProperty =
        AvaloniaProperty.Register<FilterSectionHeader, ICommand?>(nameof(SavePresetCommand));

    public ICommand? SavePresetCommand
    {
        get => GetValue(SavePresetCommandProperty);
        set => SetValue(SavePresetCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> LoadPresetCommandProperty =
        AvaloniaProperty.Register<FilterSectionHeader, ICommand?>(nameof(LoadPresetCommand));

    public ICommand? LoadPresetCommand
    {
        get => GetValue(LoadPresetCommandProperty);
        set => SetValue(LoadPresetCommandProperty, value);
    }
}
