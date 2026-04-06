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
using ReactiveUI;

namespace SonicEddy.Controls.MixerControls;

public class PluginPagesSelector : StackPanel
{
    public PluginPagesSelector()
    {
        Orientation = Orientation.Horizontal;

        if (PluginPagesCounts is not null)
            UpdateButtons(PluginPagesCounts);
    }

    public static readonly StyledProperty<
            ObservableCollection<PluginPageSelectorPluginPageCount>?>
        PluginPagesCountsProperty = AvaloniaProperty
            .Register<PluginPagesSelector,
                ObservableCollection<PluginPageSelectorPluginPageCount>?>(
                nameof(PluginPagesCounts));

    public ObservableCollection<PluginPageSelectorPluginPageCount>?
        PluginPagesCounts
    {
        get => GetValue(PluginPagesCountsProperty);
        set => SetValue(PluginPagesCountsProperty, value);
    }

    public static readonly StyledProperty<PluginPageSelectorSelectedPage?>
        SelectedPageProperty =
            AvaloniaProperty
                .Register<PluginPagesSelector, PluginPageSelectorSelectedPage
                    ?>(nameof(SelectedPage),
                    defaultValue: null,
                    defaultBindingMode: BindingMode.TwoWay);

    public PluginPageSelectorSelectedPage? SelectedPage
    {
        get => GetValue(SelectedPageProperty);
        set => SetValue(SelectedPageProperty, value);
    }

    private readonly List<Button> _buttons = [];

    private void UpdateButtons(
        IList<PluginPageSelectorPluginPageCount> pluginPageCounts)
    {
        foreach (var button in _buttons)
        {
            Children.Remove(button);
        }

        foreach (var pluginPageCount in pluginPageCounts)
        {
            for (var i = 0; i < pluginPageCount.TotalNumberOfPages; i++)
            {
                var button = new Button();
                if (i == 0)
                {
                    button.Content = pluginPageCount.Name;
                    button.Margin = new Thickness(0, 2, 2, 2);
                }
                else
                {
                    button.Content = i;
                    button.Margin = new Thickness(2);
                }


                button.CommandParameter =
                    new PluginPageSelectorSelectedPage(pluginPageCount.Name, i);

                button.Command =
                    ReactiveCommand.Create<PluginPageSelectorSelectedPage>(
                        PageSelectedAction);

                _buttons.Add(button);
                Children.Add(button);
            }
        }
    }

    private void UpdateSelectedButtonStyle(
        PluginPageSelectorSelectedPage selectedPage)
    {
        if (PluginPagesCounts is null || !PluginPagesCounts.Any()) return;

        var selectedPageCount =
            PluginPagesCounts.First(p => p.Name == selectedPage.Name);

        var indexOfSelectedPageCount =
            PluginPagesCounts.IndexOf(selectedPageCount);

        var pagesBefore =
            PluginPagesCounts.Where((p, i) => i < indexOfSelectedPageCount);

        var indexOfPagesBefore =
            pagesBefore.Aggregate(0,
                (long acc, PluginPageSelectorPluginPageCount x) =>
                    acc + x.TotalNumberOfPages);


        var activeIndex = indexOfPagesBefore + selectedPage.PageNumber;

        var index = 0;
        foreach (var button in _buttons)
        {
            button.IsEnabled = index != activeIndex;
            index++;
        }
    }

    private void PageSelectedAction(
        PluginPageSelectorSelectedPage selectedPluginPage)
    {
        SelectedPage = selectedPluginPage;
    }

    private IDisposable? _pageCountsChangedEventHandler;

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PluginPagesCountsProperty)
        {
            _pageCountsChangedEventHandler?.Dispose();
            _pageCountsChangedEventHandler = null;

            if (change.NewValue is not
                ObservableCollection<PluginPageSelectorPluginPageCount>
                pageCounts) return;

            _pageCountsChangedEventHandler =
                pageCounts.WeakSubscribe(OnPageCountsChangedEvent);

            UpdateButtons(pageCounts);
        }
        else if (change.Property == SelectedPageProperty)
        {
            if (change.NewValue is not PluginPageSelectorSelectedPage
                selectedPage)
                return;

            UpdateSelectedButtonStyle(selectedPage);
        }
    }

    private void OnPageCountsChangedEvent(object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        if (PluginPagesCounts is null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            UpdateButtons(PluginPagesCounts));
    }
}