using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class ParameterSection : Grid
{
    public ParameterSection()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*,*");
        RowDefinitions = RowDefinitions.Parse("Auto,40,40");

        var header = new TextBlock()
        {
            FontSize = 10,
            [!TextBlock.TextProperty] = this[!TextProperty],
            Margin = new(6),
            Foreground = BrushForHighlightState(IsMidiControlled)
        };

        SetRow(header, 0);
        SetColumn(header, 0);
        SetColumnSpan(header, 2);

        Children.Add(header);

        _headerText = header;

        _sliders =
        [
            new()
            {
                Background = Brushes.DimGray,
                Margin = new(2),
                IsVisible = false,
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new(2),
                IsVisible = false,
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new(2),
                IsVisible = false,
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new(2),
                IsVisible = false,
            },
        ];

        SetRow(_sliders[0], 1);
        SetColumn(_sliders[0], 0);

        SetRow(_sliders[1], 1);
        SetColumn(_sliders[1], 1);

        SetRow(_sliders[2], 2);
        SetColumn(_sliders[2], 0);

        SetRow(_sliders[3], 2);
        SetColumn(_sliders[3], 1);

        Children.AddRange(_sliders);
    }

    private TextBlock _headerText;

    private readonly IImmutableSolidColorBrush _normalBrush =
        Brushes.White;

    private readonly IImmutableSolidColorBrush _highlightBrush = Brushes.Cyan;


    public static readonly StyledProperty<bool> IsMidiControlledProperty =
        AvaloniaProperty.Register<ParameterSection, bool>(
            nameof(IsMidiControlled));

    private IImmutableSolidColorBrush BrushForHighlightState(bool state) =>
        state ? _highlightBrush : _normalBrush;

    public bool IsMidiControlled
    {
        get => GetValue(IsMidiControlledProperty);
        set => SetValue(IsMidiControlledProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<IParameter>?>
        ParametersProperty =
            AvaloniaProperty
                .Register<ParameterSection, ObservableCollection<IParameter>?>(
                    nameof(Parameters));

    public ObservableCollection<IParameter>? Parameters
    {
        get => GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ParameterSection, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private readonly ValueSlider[] _sliders;

    private IDisposable? _parametersCollectionChangeSubscription;

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ParametersProperty)
        {
            _parametersCollectionChangeSubscription?.Dispose();
            _parametersCollectionChangeSubscription = null;

            if (change.NewValue is not ObservableCollection<IParameter>
                parameters) return;

            _parametersCollectionChangeSubscription =
                parameters.WeakSubscribe(OnPropertiesCollectionChanged);

            UpdateAllSliderBindings(parameters);
        }
        else if (change.Property == IsMidiControlledProperty)
        {
            if (change.NewValue is bool isMidiControlled)
            {
                _headerText.Foreground =
                    BrushForHighlightState(isMidiControlled);
            }
        }
    }

    private void UpdateAllSliderBindings(IEnumerable<IParameter> parameters)
    {
        var firstMainParameters =
            parameters.Where(p => p.IsMainParameter).ToArray();

        var pairsToProcess = _sliders.Select((s, i) =>
            (s,
                i < firstMainParameters.Length
                    ? firstMainParameters[i]
                    : null));

        foreach ((ValueSlider s, IParameter? p) valueTuple in
                 pairsToProcess)
        {
            var (s, p) = valueTuple;

            if (p is null)
            {
                s.IsVisible = false;
            }
            else
            {
                s.IsVisible = true;
                s.Text = p.Name;
                s.Maximum = p.Maximum;
                s.Minimum = p.Minimum;

                s.Bind(ValueSlider.ValueProperty, new Binding("Value")
                {
                    Source = p,
                    Mode = BindingMode.TwoWay
                });
            }
        }
    }

    private void UpdateAllSliderBindings()
    {
        if (Parameters is not null)
            UpdateAllSliderBindings(Parameters);
    }

    private void OnPropertiesCollectionChanged(object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateAllSliderBindings);
    }
}