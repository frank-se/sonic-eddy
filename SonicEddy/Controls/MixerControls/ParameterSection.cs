using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class ParameterSection : Grid
{
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
        AvaloniaProperty.Register<ChannelHeader, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private readonly Button _headerButton;
    private readonly ValueSlider[] _sliders;

    public ParameterSection()
    {
        ColumnDefinitions = ColumnDefinitions.Parse("*,*");
        RowDefinitions = RowDefinitions.Parse("40,40,40");

        _headerButton = new()
        {
            Foreground = Brushes.White,
            [!ContentControl.ContentProperty] = this[!TextProperty],
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
        SetColumnSpan(_headerButton, 2);

        Children.Add(_headerButton);

        _sliders =
        [
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
                IsVisible = false,
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
                IsVisible = false,
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
                IsVisible = false,
            },
            new()
            {
                Background = Brushes.DimGray,
                Margin = new Thickness(2),
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

            UpdateAllSliderBindings(parameters);
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

                s.Bind(ValueSlider.TextProperty, new Binding
                {
                    Source = p.Name,
                    Mode = BindingMode.OneTime
                });

                s.Bind(ValueSlider.MaximumProperty, new Binding
                {
                    Source = p.Maximum,
                    Mode = BindingMode.OneTime
                });

                s.Bind(ValueSlider.MinimumProperty, new Binding
                {
                    Source = p.Minimum,
                    Mode = BindingMode.OneTime
                });

                s.Bind(ValueSlider.ValueProperty, new Binding
                {
                    Source = p.Value,
                    Mode = BindingMode.TwoWay
                });
            }
        }
    }

    private void UpdateAllSliderBindings()
    {
        UpdateAllSliderBindings(Parameters);
    }

    private void OnPropertiesCollectionChanged(object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateAllSliderBindings);
    }
}