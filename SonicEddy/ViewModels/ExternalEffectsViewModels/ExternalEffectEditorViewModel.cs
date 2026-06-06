using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Contracts.ExternalEffects;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.ExternalEffectsViewModels;

public sealed class ExternalEffectEditorViewModel : ViewModelBase
{
    private readonly IWireplumberService _wireplumber;

    public ExternalEffectEditorViewModel(IWireplumberService wireplumber,
        ExternalEffectConfig? existing = null)
    {
        _wireplumber = wireplumber;
        InputNodes.AddRange(FrSonic.NodeRegistry.Objects.Where(node =>
            wireplumber.IsCaptureNode(node) && string.IsNullOrEmpty(node.Pmx.Tag)));
        OutputNodes.AddRange(FrSonic.NodeRegistry.Objects.Where(node =>
            wireplumber.IsPlaybackNode(node) && string.IsNullOrEmpty(node.Pmx.Tag)));

        this.WhenAnyValue(viewModel => viewModel.SelectedInputNode)
            .Subscribe(node => RefreshPorts(node, InputPorts, "in"));
        this.WhenAnyValue(viewModel => viewModel.SelectedOutputNode)
            .Subscribe(node => RefreshPorts(node, OutputPorts, "out"));

        if (existing is not null)
            Load(existing);
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<Node> InputNodes { get; } = [];
    public ObservableCollection<Node> OutputNodes { get; } = [];
    public ObservableCollection<Port> InputPorts { get; } = [];
    public ObservableCollection<Port> OutputPorts { get; } = [];

    public Node? SelectedInputNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Port? SelectedInputLeft
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Port? SelectedInputRight
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Node? SelectedOutputNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Port? SelectedOutputLeft
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Port? SelectedOutputRight
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        SelectedInputNode is not null &&
        SelectedOutputNode is not null &&
        SelectedInputLeft is not null &&
        SelectedInputRight is not null &&
        SelectedInputLeft.ObjectId != SelectedInputRight.ObjectId &&
        SelectedOutputLeft is not null &&
        SelectedOutputRight is not null &&
        SelectedOutputLeft.ObjectId != SelectedOutputRight.ObjectId;

    public bool DialogResult { get; private set; }
    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task Save()
    {
        if (!IsValid) return;
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public async Task Cancel()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    private void RefreshPorts(Node? node, ObservableCollection<Port> target,
        string direction)
    {
        target.Clear();
        if (node is null) return;
        target.AddRange(_wireplumber.GetPortsForNode(node).Where(port =>
            port.Direction == direction));
    }

    private void Load(ExternalEffectConfig existing)
    {
        Name = existing.Name;
        SelectedInputNode = InputNodes.FirstOrDefault(node =>
            node.Name == existing.InputNodeName);
        SelectedOutputNode = OutputNodes.FirstOrDefault(node =>
            node.Name == existing.OutputNodeName);
        if (existing.InputPorts.Count == 2)
        {
            SelectedInputLeft = FindPort(InputPorts, existing.InputPorts[0]);
            SelectedInputRight = FindPort(InputPorts, existing.InputPorts[1]);
        }
        if (existing.OutputPorts.Count == 2)
        {
            SelectedOutputLeft = FindPort(OutputPorts, existing.OutputPorts[0]);
            SelectedOutputRight = FindPort(OutputPorts, existing.OutputPorts[1]);
        }
    }

    private static Port? FindPort(IEnumerable<Port> ports,
        ExternalEffectPortConfig config) => ports.FirstOrDefault(port =>
        port.Name == config.Name || port.Alias == config.Alias ||
        port.Channel == config.Channel);
}
