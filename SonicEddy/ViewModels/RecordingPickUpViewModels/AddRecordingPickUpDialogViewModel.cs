using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.RecordingPickUpViewModels;

public record PickUpLayerItem(int Index)
{
    public override string ToString() => $"Layer {Index + 1}";
}

public record PickUpChannelTypeItem(MonitoringChannelType Value, string Label)
{
    public override string ToString() => Label;
}

public record PickUpChannelIndexItem(int Index, string Label)
{
    public override string ToString() => Label;
}

public record PickUpPositionItem(MonitoringSource Value, string Label)
{
    public override string ToString() => Label;
}

public class AddRecordingPickUpDialogViewModel : ViewModelBase, IDisposable
{
    private static readonly PickUpLayerItem[] AllLayers =
    [
        new(0), new(1)
    ];

    private static readonly PickUpChannelTypeItem[] AllChannelTypes =
    [
        new(MonitoringChannelType.Strip,  "Channel"),
        new(MonitoringChannelType.Group,  "Group"),
        new(MonitoringChannelType.Master, "Master"),
        new(MonitoringChannelType.Return, "Return")
    ];

    private static readonly PickUpPositionItem[] AllPositions =
    [
        new(MonitoringSource.Pre,          "Pre"),
        new(MonitoringSource.Post,         "Post"),
        new(MonitoringSource.OutPreFader,  "Out Pre-fader"),
        new(MonitoringSource.OutPostFader, "Out Post-fader")
    ];

    private readonly CompositeDisposable _disposable = new();

    public AddRecordingPickUpDialogViewModel(ObservableCollection<Node> destNodes)
    {
        DestNodes = destNodes;
        Layers = new ObservableCollection<PickUpLayerItem>(AllLayers);
        ChannelTypes = new ObservableCollection<PickUpChannelTypeItem>(AllChannelTypes);
        ChannelIndices = [];
        Positions = new ObservableCollection<PickUpPositionItem>(AllPositions);

        this.WhenAnyValue(x => x.Name)
            .Subscribe(_ => IsButtonEnabled = IsValid)
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedChannelType)
            .Subscribe(RebuildChannelIndices)
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedLayer, x => x.SelectedChannelType,
                x => x.SelectedChannelIndex, x => x.SelectedPosition, x => x.SelectedDestNode)
            .Subscribe(_ => IsButtonEnabled = IsValid)
            .DisposeWith(_disposable);
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<PickUpLayerItem> Layers { get; }
    public PickUpLayerItem? SelectedLayer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<PickUpChannelTypeItem> ChannelTypes { get; }
    public PickUpChannelTypeItem? SelectedChannelType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<PickUpChannelIndexItem> ChannelIndices { get; }
    public PickUpChannelIndexItem? SelectedChannelIndex
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ChannelIndexVisible
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public ObservableCollection<PickUpPositionItem> Positions { get; }
    public PickUpPositionItem? SelectedPosition
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<Node> DestNodes { get; }
    public Node? SelectedDestNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsValid =>
        Name != string.Empty &&
        SelectedLayer is not null &&
        SelectedChannelType is not null &&
        (SelectedChannelType.Value == MonitoringChannelType.Master || SelectedChannelIndex is not null) &&
        SelectedPosition is not null &&
        SelectedDestNode is not null;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public MonitoringChannelKey BuildSourceKey()
    {
        var channelIndex = SelectedChannelType!.Value == MonitoringChannelType.Master
            ? 0
            : SelectedChannelIndex!.Index;
        return new MonitoringChannelKey(SelectedLayer!.Index, SelectedChannelType.Value, channelIndex);
    }

    public bool DialogResult;

    public async Task Add()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public async Task Cancel()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public Interaction<Unit, Unit> Close { get; } = new();

    private void RebuildChannelIndices(PickUpChannelTypeItem? type)
    {
        ChannelIndices.Clear();
        SelectedChannelIndex = null;

        if (type is null)
        {
            ChannelIndexVisible = false;
            return;
        }

        if (type.Value == MonitoringChannelType.Master)
        {
            ChannelIndexVisible = false;
            return;
        }

        var count = type.Value switch
        {
            MonitoringChannelType.Strip  => 8,
            MonitoringChannelType.Group  => 4,
            MonitoringChannelType.Return => 4,
            _                            => 0
        };
        var prefix = type.Value switch
        {
            MonitoringChannelType.Strip  => "CH",
            MonitoringChannelType.Group  => "GR",
            MonitoringChannelType.Return => "RT",
            _                            => ""
        };
        for (var i = 0; i < count; i++)
            ChannelIndices.Add(new PickUpChannelIndexItem(i, $"{prefix}{i + 1}"));

        ChannelIndexVisible = true;
    }

    public void Dispose()
    {
        _disposable.Dispose();
        GC.SuppressFinalize(this);
    }
}
