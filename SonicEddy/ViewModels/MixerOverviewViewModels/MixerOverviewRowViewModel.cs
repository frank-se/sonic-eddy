using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.ViewModels.MixerOverviewViewModels;

// Thin, non-owning wrapper around an existing IChannelStrip/IGroupChannel/IReturnChannel.
// Text/PanAndVolume/AudioFrom/Looper are computed straight from the wrapped channel rather
// than copied, so they can never drift from the live mixer state — Looper in particular is
// the same LooperSectionViewModel instance on both strip and group channels, so its own Mix
// property can be bound to directly (Looper.Mix) without forwarding. AudioTo is deliberately
// not exposed for display — a row's Cluster/position already conveys where it routes to
// (grouped directly under its target), so a separate label would just repeat that. SendNTrim
// and Trim are the exception: IChannelStrip/IGroupChannel declare Send1..4Trim separately (no
// shared interface), and Trim only exists on IChannelStrip, so each is forwarded via
// WhenAnyValue into a single bindable property here, keeping the bars live without a full
// row-list rebuild on every change (rebuilds only happen on routing changes, see
// MixerOverviewLayerViewModel.Rebuild).
public sealed class MixerOverviewRowViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public MixerOverviewRowViewModel(IChannel channel, MixerOverviewRowKind kind,
        MixerOverviewCluster cluster)
    {
        Channel = channel;
        Kind = kind;
        Cluster = cluster;

        switch (channel)
        {
            case IChannelStrip strip:
                HasSends = true;
                HasTrim = true;
                Looper = strip.Looper;
                AudioFromName = strip.SelectedAudioFromRoutingTarget?.Name;
                strip.WhenAnyValue(s => s.Trim).Subscribe(v => Trim = v).DisposeWith(_disposables);
                strip.WhenAnyValue(s => s.Send1Trim).Subscribe(v => Send1Trim = v).DisposeWith(_disposables);
                strip.WhenAnyValue(s => s.Send2Trim).Subscribe(v => Send2Trim = v).DisposeWith(_disposables);
                strip.WhenAnyValue(s => s.Send3Trim).Subscribe(v => Send3Trim = v).DisposeWith(_disposables);
                strip.WhenAnyValue(s => s.Send4Trim).Subscribe(v => Send4Trim = v).DisposeWith(_disposables);
                break;
            case IGroupChannel group:
                HasSends = true;
                Looper = group.Looper;
                group.WhenAnyValue(g => g.Send1Trim).Subscribe(v => Send1Trim = v).DisposeWith(_disposables);
                group.WhenAnyValue(g => g.Send2Trim).Subscribe(v => Send2Trim = v).DisposeWith(_disposables);
                group.WhenAnyValue(g => g.Send3Trim).Subscribe(v => Send3Trim = v).DisposeWith(_disposables);
                group.WhenAnyValue(g => g.Send4Trim).Subscribe(v => Send4Trim = v).DisposeWith(_disposables);
                break;
        }
    }

    public IChannel Channel { get; }
    public MixerOverviewRowKind Kind { get; }
    public MixerOverviewCluster Cluster { get; }
    public bool HasSends { get; }
    public bool HasTrim { get; }
    public LooperSectionViewModel? Looper { get; }
    public bool HasLooper => Looper is not null;

    // Hidden columns use Opacity rather than IsVisible so their width stays reserved —
    // otherwise a collapsed element (e.g. Trim on a group row) shifts every column after it
    // out from under its header.
    public double SendsOpacity => HasSends ? 1.0 : 0.0;
    public double TrimOpacity => HasTrim ? 1.0 : 0.0;
    public double LooperOpacity => HasLooper ? 1.0 : 0.0;

    public bool IsDimmed => Cluster == MixerOverviewCluster.Unrouted;
    public double DimOpacity => IsDimmed ? 0.45 : 1.0;

    public string Text => Cluster == MixerOverviewCluster.Group &&
                           Kind == MixerOverviewRowKind.ChannelStrip
        ? "  " + Channel.Text
        : Channel.Text;

    public IPanAndVolume PanAndVolume => Channel.PanAndVolume;

    public string? AudioFromName { get; }

    public string AccentColor => Kind switch
    {
        MixerOverviewRowKind.ChannelStrip => "MidnightBlue",
        MixerOverviewRowKind.GroupChannel => "Indigo",
        MixerOverviewRowKind.ReturnChannel => "BlueViolet",
        _ => "Gray"
    };

    public double Trim
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send1Trim
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send2Trim
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send3Trim
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Send4Trim
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void Dispose() => _disposables.Dispose();
}
