using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.VirtualOutputsViewModels;

public sealed class AddVirtualOutputDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposable = new();

    public AddVirtualOutputDialogViewModel(
        ObservableCollection<Node> potentialNodes,
        IWireplumberService wireplumberService)
    {
        PotentialNodes = potentialNodes;

        this.WhenAnyValue(viewModel => viewModel.Name)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);

        this.WhenAnyValue(viewModel => viewModel.SelectedNode)
            .Subscribe(node =>
            {
                SelectedLeftPort = null;
                SelectedRightPort = null;
                PotentialPorts.Clear();

                if (node is not null)
                    PotentialPorts.AddRange(
                        wireplumberService.GetPortsForNode(node));

                ValidateForm();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(viewModel => viewModel.SelectedLeftPort)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);

        this.WhenAnyValue(viewModel => viewModel.SelectedRightPort)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<Node> PotentialNodes { get; }

    public Node? SelectedNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<Port> PotentialPorts { get; } = [];

    public Port? SelectedLeftPort
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Port? SelectedRightPort
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool DialogResult;

    public bool IsValid => SelectedNode is not null &&
                           SelectedLeftPort is not null &&
                           SelectedRightPort is not null &&
                           Name != string.Empty;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task Cancel()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task Add()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    private void ValidateForm() => IsButtonEnabled = IsValid;

    public void Dispose()
    {
        _disposable.Dispose();
        GC.SuppressFinalize(this);
    }
}
