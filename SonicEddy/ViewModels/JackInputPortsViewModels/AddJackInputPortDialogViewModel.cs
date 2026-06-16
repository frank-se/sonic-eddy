using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.JackInputPortsViewModels;

public class AddJackInputPortDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposable = new();
    private readonly IWireplumberService _wireplumberService;

    public AddJackInputPortDialogViewModel(IWireplumberService wireplumberService)
    {
        _wireplumberService = wireplumberService;
        PotentialNodes = new(wireplumberService.GetJackNodes());

        this.WhenAnyValue(x => x.Name)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedNode)
            .Subscribe(node =>
            {
                SelectedLeftPort = null;
                SelectedRightPort = null;
                PotentialPorts.Clear();

                if (node is null) return;

                PotentialPorts.AddRange(
                    wireplumberService.GetPortsForNode(node)
                        .Where(p => p.Direction == "out"));

                ValidateForm();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedLeftPort)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedRightPort)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.IsMono)
            .Subscribe(_ =>
            {
                SelectedRightPort = null;
                ValidateForm();
            })
            .DisposeWith(_disposable);
    }

    private void ValidateForm() => IsButtonEnabled = IsValid;

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

    public bool IsMono
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsValid => Name != string.Empty &&
                           SelectedNode is not null &&
                           SelectedLeftPort is not null &&
                           (IsMono || SelectedRightPort is not null);

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool DialogResult;

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
