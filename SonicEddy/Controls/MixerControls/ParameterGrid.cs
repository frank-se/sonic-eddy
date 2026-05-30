using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public class ParameterGrid : Grid
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ParameterGrid, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<int> NumbersOfColumnsProperty =
        AvaloniaProperty.Register<ParameterGrid, int>(
            nameof(NumberOfColumns),
            defaultValue: 4);

    public int NumberOfColumns
    {
        get => GetValue(NumbersOfColumnsProperty);
        set => SetValue(NumbersOfColumnsProperty, value);
    }

    public static readonly StyledProperty<int> NumbersOfRowsProperty =
        AvaloniaProperty.Register<ParameterGrid, int>(
            nameof(NumberOfColumns),
            defaultValue: 4);

    public int NumberOfRows
    {
        get => GetValue(NumbersOfRowsProperty);
        set => SetValue(NumbersOfRowsProperty, value);
    }

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<ParameterGrid, int>(
            nameof(CurrentPage),
            defaultValue: 0);

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<IParameter>?>
        ParametersProperty =
            AvaloniaProperty
                .Register<ParameterGrid, ObservableCollection<IParameter>?>(
                    nameof(Parameters));

    public ObservableCollection<IParameter>? Parameters
    {
        get => GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    private TextBlock _header;

    public ParameterGrid()
    {
        _header = new TextBlock()
        {
            FontSize = 10,
            [!TextBlock.TextProperty] = this[!TextProperty],
            Margin = new Thickness(6)
        };

        SetRow(_header, 0);
        SetColumn(_header, 0);

        Children.Add(_header);
        
        SetupGridAndSliders(NumberOfRows, NumberOfColumns);
        UpdateAllBindings();
    }

    private IDisposable? _parametersChangedHandler;

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == NumbersOfColumnsProperty)
        {
            if (change.NewValue is not int numberOfColumns) return;
            SetupGridAndSliders(NumberOfRows, numberOfColumns);
            UpdateAllBindings();
        }
        else if (change.Property == NumbersOfRowsProperty)
        {
            if (change.NewValue is not int numberOfRows) return;
            SetupGridAndSliders(numberOfRows, NumberOfColumns);
            UpdateAllBindings();
        }
        else if (change.Property == ParametersProperty)
        {
            _parametersChangedHandler?.Dispose();
            _parametersChangedHandler = null;

            if (change.NewValue is not ObservableCollection<IParameter>
                parameters) return;

            _parametersChangedHandler =
                parameters.WeakSubscribe(OnParametersChanged);

            UpdateAllBindings();
        }
        else if (change.Property == CurrentPageProperty)
        {
            UpdateAllBindings();
        }
    }

    private void OnParametersChanged(object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateAllBindings);
    }

    private void UpdateAllBindings()
    {
        var slidersPerPage = NumberOfRows * NumberOfColumns;
        var startIndex = CurrentPage * slidersPerPage;

        for (var row = 0; row < NumberOfRows; row++)
        {
            for (var column = 0; column < NumberOfColumns; column++)
            {
                var sliderIndex = row * NumberOfColumns + column;
                var parameterIndex = sliderIndex + startIndex;
                var slider = _sliders[sliderIndex];
                if (parameterIndex < Parameters?.Count)
                {
                    var p = Parameters[parameterIndex];
                    slider.IsVisible = true;

                    slider.Text = p.Name;
                    slider.Maximum = p.Maximum;
                    slider.Minimum = p.Minimum;

                    slider.Bind(ValueSlider.ValueProperty, new Binding("Value")
                    {
                        Source = p,
                        Mode = BindingMode.TwoWay
                    });
                }
                else
                {
                    slider.IsVisible = false;
                }
            }
        }
    }

    private readonly List<ValueSlider> _sliders = [];

    private void SetupGridAndSliders(int numberOfRows, int numberOfColumns)
    {
        if (numberOfColumns > 0)
            SetColumnSpan(_header, numberOfColumns);
        
        if (numberOfRows == 0 || numberOfColumns == 0)
        {
            foreach (var slider in _sliders)
            {
                Children.Remove(slider);
            }

            _sliders.Clear();
            return;
        }

        RowDefinitions.Clear();
        RowDefinitions.Add(new() { Height = GridLength.Auto });

        for (var i = 0; i < numberOfRows; i++)
        {
            RowDefinitions.Add(new()
            {
                Height = GridLength.Star
            });
        }

        ColumnDefinitions.Clear();
        for (var i = 0; i < numberOfColumns; i++)
        {
            ColumnDefinitions.Add(new()
            {
                Width = GridLength.Star
            });
        }

        var numberOfCells = numberOfRows * numberOfColumns;

        if (_sliders.Count > numberOfCells)
        {
            var toDelete = _sliders.Count - numberOfCells;
            foreach (var slider in _sliders.Take(toDelete))
            {
                Children.Remove(slider);
            }

            _sliders.RemoveRange(0, toDelete);
        }
        else if (_sliders.Count < numberOfCells)
        {
            var toCreate = numberOfCells - _sliders.Count;
            for (var i = 0; i < toCreate; i++)
            {
                _sliders.Add(new()
                {
                    Background = Brushes.DimGray,
                    Margin = new Thickness(4)
                });
                Children.Add(_sliders.Last());
            }
        }

        for (var row = 0; row < numberOfRows; row++)
        {
            for (var column = 0; column < numberOfColumns; column++)
            {
                var index = row * numberOfColumns + column;
                var slider = _sliders[index];
                SetRow(slider, row + 1);
                SetColumn(slider, column);
            }
        }
    }
}