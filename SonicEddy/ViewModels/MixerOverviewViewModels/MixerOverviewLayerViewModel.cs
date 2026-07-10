using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.ViewModels.MixerOverviewViewModels;

// Builds and reactively maintains one layer's row list for the overview screen. Never owns or
// disposes the wrapped MixerLayerViewModel/its channel view models — those are shared,
// app-lifetime instances owned by MainWindowViewModel. Dispose() here only tears down the
// subscriptions this class created (routing-change listeners + the row wrappers' own
// forwarded-send subscriptions).
public sealed class MixerOverviewLayerViewModel : IDisposable
{
    private readonly MixerLayerViewModel _layer;
    private CompositeDisposable _routingSubscriptions = new();

    public MixerOverviewLayerViewModel(MixerLayerViewModel layer)
    {
        _layer = layer;

        if (_layer.ChannelStrips is not null)
            _layer.ChannelStrips.CollectionChanged += OnCollectionChanged;
        if (_layer.GroupChannels is not null)
            _layer.GroupChannels.CollectionChanged += OnCollectionChanged;
        if (_layer.ReturnChannels is not null)
            _layer.ReturnChannels.CollectionChanged += OnCollectionChanged;

        RewireAndRebuild();
    }

    public ObservableCollection<MixerOverviewRowViewModel> Rows { get; } = [];

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RewireAndRebuild();

    private void RewireAndRebuild()
    {
        _routingSubscriptions.Dispose();
        _routingSubscriptions = new CompositeDisposable();

        foreach (var strip in _layer.ChannelStrips ?? [])
        {
            strip.WhenAnyValue(s => s.SelectedAudioToRoutingTarget)
                .Subscribe(_ => Rebuild())
                .DisposeWith(_routingSubscriptions);
            strip.WhenAnyValue(s => s.SelectedAudioFromRoutingTarget)
                .Subscribe(_ => Rebuild())
                .DisposeWith(_routingSubscriptions);
        }

        foreach (var group in _layer.GroupChannels ?? [])
        {
            group.WhenAnyValue(g => g.SelectedAudioToRoutingTarget)
                .Subscribe(_ => Rebuild())
                .DisposeWith(_routingSubscriptions);
        }

        Rebuild();
    }

    private void Rebuild()
    {
        var strips = _layer.ChannelStrips ?? [];
        var groups = _layer.GroupChannels ?? [];
        var returns = _layer.ReturnChannels ?? [];

        var newRows = new List<MixerOverviewRowViewModel>();

        newRows.AddRange(strips
            .Where(s => s.SelectedAudioToRoutingTarget?.Channel is IMasterChannel)
            .OrderBy(s => s.ChannelId)
            .Select(s => new MixerOverviewRowViewModel(s,
                MixerOverviewRowKind.ChannelStrip, MixerOverviewCluster.MasterBound)));

        foreach (var group in groups.OrderBy(g => g.ChannelId))
        {
            newRows.Add(new MixerOverviewRowViewModel(group,
                MixerOverviewRowKind.GroupChannel, MixerOverviewCluster.Group));

            newRows.AddRange(strips
                .Where(s => ReferenceEquals(s.SelectedAudioToRoutingTarget?.Channel, group))
                .OrderBy(s => s.ChannelId)
                .Select(s => new MixerOverviewRowViewModel(s,
                    MixerOverviewRowKind.ChannelStrip, MixerOverviewCluster.Group)));
        }

        newRows.AddRange(strips
            .Where(s => s.SelectedAudioToRoutingTarget is null)
            .OrderBy(s => s.ChannelId)
            .Select(s => new MixerOverviewRowViewModel(s,
                MixerOverviewRowKind.ChannelStrip, MixerOverviewCluster.Unrouted)));

        newRows.AddRange(returns
            .Select(r => new MixerOverviewRowViewModel(r,
                MixerOverviewRowKind.ReturnChannel, MixerOverviewCluster.Return)));

        foreach (var row in Rows)
            row.Dispose();
        Rows.Clear();
        foreach (var row in newRows)
            Rows.Add(row);
    }

    public void Dispose()
    {
        if (_layer.ChannelStrips is not null)
            _layer.ChannelStrips.CollectionChanged -= OnCollectionChanged;
        if (_layer.GroupChannels is not null)
            _layer.GroupChannels.CollectionChanged -= OnCollectionChanged;
        if (_layer.ReturnChannels is not null)
            _layer.ReturnChannels.CollectionChanged -= OnCollectionChanged;

        _routingSubscriptions.Dispose();

        foreach (var row in Rows)
            row.Dispose();
        Rows.Clear();
    }
}
