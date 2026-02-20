using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;

namespace SonicEddy.Controls.MixerControls;

public class ParametersEditor : Grid
{
    public static readonly
        StyledProperty<List<ParameterCollection>?>
        ParameterCollectionsProperty =
            AvaloniaProperty
                .Register<ParametersEditor,
                    List<ParameterCollection>?>(
                    nameof(ParameterCollections));

    public List<ParameterCollection>? ParameterCollections
    {
        get => GetValue(ParameterCollectionsProperty);
        set => SetValue(ParameterCollectionsProperty, value);
    }

    public static readonly StyledProperty<int> NumberOfColumnsProperty =
        AvaloniaProperty.Register<ParametersEditor, int>(
            nameof(NumberOfColumns),
            defaultValue: 4);

    public int NumberOfColumns
    {
        get => GetValue(NumberOfColumnsProperty);
        set => SetValue(NumberOfColumnsProperty, value);
    }

    public static readonly StyledProperty<int> NumberOfRowsProperty =
        AvaloniaProperty.Register<ParametersEditor, int>(
            nameof(NumberOfColumns),
            defaultValue: 4);

    public int NumberOfRows
    {
        get => GetValue(NumberOfRowsProperty);
        set => SetValue(NumberOfRowsProperty, value);
    }

    public static readonly StyledProperty<PluginPageSelectorSelectedPage?>
        SelectedPageProperty =
            AvaloniaProperty
                .Register<ParametersEditor, PluginPageSelectorSelectedPage
                    ?>(nameof(SelectedPage),
                    defaultValue: null,
                    defaultBindingMode: BindingMode.TwoWay);

    public PluginPageSelectorSelectedPage? SelectedPage
    {
        get => GetValue(SelectedPageProperty);
        set => SetValue(SelectedPageProperty, value);
    }

    private readonly List<ParameterGrid> _parameterGrids = [];
    private readonly PluginPagesSelector _pagesSelector;

    public ParametersEditor()
    {
        RowDefinitions = RowDefinitions.Parse("*,Auto");
        
        _pagesSelector = new()
        {
            Margin = new Thickness(0, 8, 0, 0)
        };

        _pagesSelector.Bind(PluginPagesSelector.SelectedPageProperty,
            new Binding("SelectedPage")
            {
                Source = this,
                Mode = BindingMode.TwoWay
            });

        SetRow(_pagesSelector, 1);
        
        Children.Add(_pagesSelector);

        if (ParameterCollections is null ||
            ParameterCollections.Count == 0) return;
        
        UpdateParameterGrids();
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ParameterCollectionsProperty)
        {
            if (change.NewValue is not List<ParameterCollection>
                parameterCollections) return;

            UpdateParameterGrids();
        }
        else if (change.Property == SelectedPageProperty)
        {
            if (change.NewValue is not PluginPageSelectorSelectedPage page)
                return;

            var parameterCollection =
                ParameterCollections?.FirstOrDefault(p => p.Name == page.Name);

            if (parameterCollection is null) return;

            var pageIndex = ParameterCollections?.IndexOf(parameterCollection);

            if (pageIndex is null) return;
            for (var i = 0; i < _parameterGrids.Count; i++)
            {
                _parameterGrids[i].IsVisible = i == pageIndex;
                if (i == pageIndex)
                    _parameterGrids[i].CurrentPage = page.PageNumber;
            }
        }
    }

    private void UpdateParameterGrids()
    {
        var itemsPerPage = NumberOfRows * NumberOfColumns;

        foreach (var parameterGrid in _parameterGrids)
        {
            Children.Remove(parameterGrid);
        }

        _parameterGrids.Clear();

        var counts = new List<PluginPageSelectorPluginPageCount>();

        if (!ParameterCollections?.Any() ?? false) return;

        foreach (var parameterCollection in ParameterCollections ?? [])
        {
            var count = new PluginPageSelectorPluginPageCount(
                parameterCollection.Name,
                (uint)Math.Ceiling(
                    (double)parameterCollection.Parameters.Count /
                    itemsPerPage));
            counts.Add(count);

            var parameterGrid = new ParameterGrid()
            {
                Text = parameterCollection.Name,
                NumberOfColumns = NumberOfColumns,
                NumberOfRows = NumberOfRows,
                Parameters =
                    new ObservableCollection<IParameter>(parameterCollection
                        .Parameters)
            };

            SetRow(parameterGrid, 0);
            _parameterGrids.Add(parameterGrid);
            Children.Add(parameterGrid);
        }

        _pagesSelector.PluginPagesCounts =
            new ObservableCollection<PluginPageSelectorPluginPageCount>(counts);
        SelectedPage = new PluginPageSelectorSelectedPage(
            counts.First().Name, 0);
    }
}