using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.VirtualInputsViewModels;

public class AddVirtualInputDialogViewModel : ViewModelBase, IDisposable
{
    private CompositeDisposable _disposable = new();

    public AddVirtualInputDialogViewModel(
        ObservableCollection<Node> potentialNodes,
        IWireplumberService wireplumberService)
    {
        PotentialNodes = potentialNodes;
        var wireplumberService1 = wireplumberService;

        this.WhenAnyValue(x => x.Name)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedNode)
            .Subscribe(node =>
            {
                SelectedLeftPort = null;
                SelectedRightPort = null;
                PotentialPorts.Clear();

                if (node == null) return;

                PotentialPorts.AddRange(
                    wireplumberService1.GetPortsForNode(node));
                
                ValidateForm();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedLeftPort)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedRightPort)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);
    }

    private void ValidateForm()
    {
        IsButtonEnabled = IsValid;
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

    public bool DialogResult = false;

    public bool IsValid => SelectedNode is not null &&
                           SelectedLeftPort is not null &&
                           SelectedRightPort is not null &&
                           Name != string.Empty;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

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

    public Interaction<Unit, Unit> Close { get; } = new();

    public void Dispose()
    {
        _disposable.Dispose();

        GC.SuppressFinalize(this);
    }
}