using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class FilterSectionHeader : Grid
{
    public FilterSectionHeader()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*, 30");
        RowDefinitions = RowDefinitions.Parse("30");

        var header = new StackPanel()
        {
            Orientation = Orientation.Horizontal
        };

        var font = new FontFamily(
            "avares://SonicEddy/Assets/Fonts/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular");

        var headerIcon = new TextBlock()
        {
            Text = "\uF6A9",
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
        SetColumnSpan(header, 2);

        Children.Add(header);

        Button deleteButton = new()
        {
            Content = "\uF367",
            [!Button.CommandProperty] = this[!DeleteCommandProperty],
            [!Button.CommandParameterProperty] =
                this[!DeleteCommandParameterProperty],
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
        SetColumn(deleteButton, 1);

        Children.Add(deleteButton);

        Button addButton = new()
        {
            Content = "\uF107",
            [!Button.CommandProperty] = this[!AddCommandProperty],
            [!Button.CommandParameterProperty] =
                this[!AddCommandParameterProperty],
            FontFamily = font,
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
        };

        SetRow(addButton, 0);
        SetColumn(addButton, 1);

        var binding = new Binding("HasFilter")
        {
            Source = this,
            Path = "!HasFilter"
        };

        addButton.Bind(Button.IsVisibleProperty, binding);

        Children.Add(addButton);
    }

    public static readonly StyledProperty<bool> HasFilterProperty =
        AvaloniaProperty.Register<FilterSectionHeader, bool>(nameof(HasFilter));

    public bool HasFilter
    {
        get => GetValue(HasFilterProperty);
        set => SetValue(HasFilterProperty, value);
    }

    public static readonly StyledProperty<bool> IsMidiControlledProperty =
        AvaloniaProperty.Register<FilterSectionHeader, bool>(
            nameof(IsMidiControlled));

    public bool IsMidiControlled
    {
        get => GetValue(IsMidiControlledProperty);
        set => SetValue(IsMidiControlledProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<FilterSectionHeader, ICommand?>(
            nameof(AddCommand));

    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        AddCommandParameterProperty =
            AvaloniaProperty.Register<FilterSectionHeader, object?>(
                nameof(AddCommandParameter));

    public object? AddCommandParameter
    {
        get => GetValue(AddCommandParameterProperty);
        set => SetValue(AddCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
        AvaloniaProperty.Register<FilterSectionHeader, ICommand?>(
            nameof(DeleteCommand));

    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        DeleteCommandParameterProperty =
            AvaloniaProperty.Register<FilterSectionHeader, object?>(
                nameof(DeleteCommandParameter));

    public object? DeleteCommandParameter
    {
        get => GetValue(DeleteCommandParameterProperty);
        set => SetValue(DeleteCommandParameterProperty, value);
    }
}