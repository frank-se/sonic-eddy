using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;

namespace SonicEddy.Controls.MixerControls;

public class RoutingComboBox : Grid
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<RoutingComboBox, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<IRoutingTarget>?>
        RoutingTargetsProperty =
            AvaloniaProperty
                .Register<RoutingComboBox, ObservableCollection<IRoutingTarget>
                    ?>(nameof(RoutingTargets));

    public ObservableCollection<IRoutingTarget>? RoutingTargets
    {
        get => GetValue(RoutingTargetsProperty);
        set => SetValue(RoutingTargetsProperty, value);
    }

    public static readonly StyledProperty<IRoutingTarget?>
        SelectedRoutingTargetProperty =
            AvaloniaProperty.Register<RoutingComboBox, IRoutingTarget?>(
                nameof(SelectedRoutingTarget),
                defaultBindingMode: BindingMode.TwoWay);

    public IRoutingTarget? SelectedRoutingTarget
    {
        get => GetValue(SelectedRoutingTargetProperty);
        set => SetValue(SelectedRoutingTargetProperty, value);
    }

    private readonly ComboBox _comboBox;
    private readonly Button _clearButton;

    public ICommand ClearCommand { get; }

    public RoutingComboBox()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*,40");
        RowDefinitions = RowDefinitions.Parse("28,40");

        var textBlock = new TextBlock
        {
            FontSize = 10,
            [!TextBlock.TextProperty] = this[!TextProperty],
            Margin = new Thickness(6),
        };

        SetRow(textBlock, 0);
        SetColumn(textBlock, 0);
        SetColumnSpan(textBlock, 2);

        Children.Add(textBlock);

        _comboBox = new()
        {
            FontSize = 14,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0)
        };

        _comboBox.Bind(SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(SelectedRoutingTarget))
            {
                Source = this,
                Mode = BindingMode.TwoWay
            });

        _comboBox.ItemTemplate =
            new FuncDataTemplate<IRoutingTarget?>((item, scope) =>
            {
                var text = item is null ? "None" : item.Name;
                return new TextBlock()
                {
                    Text = text,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
            });

        SetRow(_comboBox, 1);
        SetColumn(_comboBox, 0);

        var font = new FontFamily(
            "avares://SonicEddy/Assets/Fonts/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular");

        _clearButton = new()
        {
            Content = "\uF367",
            FontFamily = font,
            FontSize = 24,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new(0),
            CornerRadius = new CornerRadius(0)
        };

        ClearCommand = ReactiveCommand.Create(() =>
        {
            SelectedRoutingTarget = null;
        });

        _clearButton.Bind(Button.CommandProperty,
            new Binding(nameof(ClearCommand))
            {
                Source = this,
                Mode = BindingMode.OneWay
            });

        SetRow(_clearButton, 1);
        SetColumn(_clearButton, 1);

        Children.Add(_comboBox);
        Children.Add(_clearButton);

        UpdateClearLayout();
    }

    private void UpdateClearLayout()
    {
        if (SelectedRoutingTarget is not null)
            SetSelectedLayout();
        else
            SetNothingSelectedLayout();
    }

    private void SetNothingSelectedLayout()
    {
        SetColumnSpan(_comboBox, 2);
        _clearButton.IsVisible = false;
    }

    private void SetSelectedLayout()
    {
        SetColumnSpan(_comboBox, 1);
        _clearButton.IsVisible = true;
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RoutingTargetsProperty)
        {
            _comboBox.Bind(ItemsControl.ItemsSourceProperty, new Binding()
            {
                Source = change.NewValue,
                Mode = BindingMode.OneWay
            });
        }

        if (change.Property == SelectedRoutingTargetProperty)
            if (change.NewValue is not null)
                SetSelectedLayout();
            else
                SetNothingSelectedLayout();
    }
}