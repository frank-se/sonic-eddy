using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class ChannelHeader : Grid
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ChannelHeader, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<ChannelHeader, ICommand?>(
            nameof(SelectCommand));

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
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

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ChannelHeader, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private Button _headerButton;
    private Button _button;

    public ChannelHeader()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*, 40");
        RowDefinitions = RowDefinitions.Parse("40");

        _headerButton = new()
        {
            Foreground = FontBrush,
            [!ContentControl.ContentProperty] = this[!TextProperty],
            FontSize = 14,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0),
            Background = NotSelectedBrush,
            [!Button.CommandProperty] = this[!SelectCommandProperty],
        };

        SetRow(_headerButton, 0);
        SetColumn(_headerButton, 0);

        var font = new FontFamily(
            "avares://SonicEddy/Assets/Fonts/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular");

        _button = new()
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
            CornerRadius = new CornerRadius(0)
        };

        Children.Add(_headerButton);
        Children.Add(_button);

        SetRow(_button, 0);
        SetColumn(_button, 1);

        UpdateSelectedVisuals(IsSelected);
    }

    private static readonly IImmutableSolidColorBrush SelectedBrush =
        Brushes.Indigo;

    private static readonly IImmutableSolidColorBrush NotSelectedBrush =
        Brushes.Black;

    private void UpdateSelectedVisuals(bool isSelected)
    {
        Background = isSelected ? SelectedBrush : NotSelectedBrush;
        _headerButton.Background =
            isSelected ? SelectedBrush : NotSelectedBrush;
    }

    private static readonly IImmutableSolidColorBrush FontBrush = Brushes.White;

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsSelectedProperty)
            if (change.NewValue is bool isSelected)
                UpdateSelectedVisuals(isSelected);
    }
}