using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class FilterSectionHeader : Grid
{
    public static readonly StyledProperty<bool> HasFilterProperty =
        AvaloniaProperty.Register<ChannelHeader, bool>(nameof(HasFilter));

    public bool HasFilter
    {
        get => GetValue(HasFilterProperty);
        set => SetValue(HasFilterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<ChannelHeader, ICommand?>(
            nameof(AddCommand));

    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        AddCommandParameterProperty =
            AvaloniaProperty.Register<ChannelHeader, object?>(
                nameof(AddCommandParameter));

    public object? AddCommandParameter
    {
        get => GetValue(AddCommandParameterProperty);
        set => SetValue(AddCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
        AvaloniaProperty.Register<ChannelHeader, ICommand?>(
            nameof(DeleteCommand));

    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        DeleteCommandParameterProperty =
            AvaloniaProperty.Register<ChannelHeader, object?>(
                nameof(DeleteCommandParameter));

    public object? DeleteCommandParameter
    {
        get => GetValue(DeleteCommandParameterProperty);
        set => SetValue(DeleteCommandParameterProperty, value);
    }

    private Button _addButton;
    private Button _deleteButton;
    private Button _headerButton;

    public FilterSectionHeader()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*, 40");
        RowDefinitions = RowDefinitions.Parse("40");

        _headerButton = new()
        {
            Foreground = Brushes.White,
            Content = "Filter",
            FontSize = 14,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
            Background = Brushes.Black,
        };

        SetRow(_headerButton, 0);
        SetColumn(_headerButton, 0);

        Children.Add(_headerButton);
        
        var font = new FontFamily(
            "avares://SonicEddy/Assets/Fonts/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular");

        _deleteButton = new()
        {
            Content = "\uF367",
            [!Button.CommandProperty] = this[!DeleteCommandProperty],
            [!Button.CommandParameterProperty] =
                this[!DeleteCommandParameterProperty],
            FontFamily = font,
            FontSize = 24,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
            [!Button.IsVisibleProperty] = this[!HasFilterProperty]
        };

        SetRow(_deleteButton, 0);
        SetColumn(_deleteButton, 1);
        
        Children.Add(_deleteButton);

        _addButton = new()
        {
            Content = "\uF107",
            [!Button.CommandProperty] = this[!AddCommandProperty],
            [!Button.CommandParameterProperty] =
                this[!AddCommandParameterProperty],
            FontFamily = font,
            FontSize = 24,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
        };

        SetRow(_addButton, 0);
        SetColumn(_addButton, 1);

        var binding = new Binding("HasFilter")
        {
            Source = this,
            Path = "!HasFilter"
        };

        _addButton.Bind(Button.IsVisibleProperty, binding);
        
        Children.Add(_addButton);
    }
}