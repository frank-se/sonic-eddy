using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.MixerViewModels;

public class AddMixerChannelViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly IWireplumberService _wireplumberService;

    public ObservableCollection<NodeViewModel> AvailableNodes { get; } = [];

    public NodeViewModel? SelectedNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AddMixerChannelViewModel(
        IWireplumberService wireplumberService,
        List<ulong> alreadyUsedNodeSerials)
    {
        _wireplumberService = wireplumberService;
        GetAvailableNodes(alreadyUsedNodeSerials);

        this.WhenAnyValue(x => x.SelectedNode)
            .Subscribe(_ => Validate())
            .DisposeWith(_disposables);
    }

    private void Validate()
    {
        IsButtonEnabled = SelectedNode != null;
    }

    private void GetAvailableNodes(List<ulong> alreadyUsedNodeSerials)
    {
        var nodes = _wireplumberService.GetPlaybackNodes();
        AvailableNodes.AddRange(nodes
            .Where(n => !alreadyUsedNodeSerials.Contains(n.ObjectSerial))
            .Select(n => new NodeViewModel()
            {
                Description = n.Name!,
                Node = n
            }));
    }

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task CancelAction()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task AddModuleAction()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public bool DialogResult;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void Dispose() => _disposables.Dispose();
}