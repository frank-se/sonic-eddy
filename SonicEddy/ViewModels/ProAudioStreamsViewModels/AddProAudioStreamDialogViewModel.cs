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
using Fr.Wireplumber;
using Fr.Wireplumber.Model;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;

namespace SonicEddy.ViewModels.ProAudioStreamsViewModels;

public class AddProAudioStreamDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private string _description = string.Empty;
    private ulong _leftPortId;
    private ulong _rightPortId;
    private Node? _selectedDeviceNode = null;
    private bool _isButtonEnabled = true;

    public AddProAudioStreamDialogViewModel()
    {
        var nodes = Wireplumber.DeviceRegistry.Objects.Select(device =>
                Wireplumber.NodeRegistry.Objects.FirstOrDefault(n =>
                    n.DeviceAssignment?.Id == device.ObjectId &&
                    n.Media.Class == "Audio/Source"))
            .OfType<Node>()
            .ToList();

        AvailableDeviceNodes.AddRange(nodes);

        this.WhenAnyValue(x => x.Name)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.LeftPortId)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.RightPortId)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedDeviceNode)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Description)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);
    }

    public bool DialogResult = false;

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public ulong LeftPortId
    {
        get => _leftPortId;
        set => this.RaiseAndSetIfChanged(ref _leftPortId, value);
    }

    public ulong RightPortId
    {
        get => _rightPortId;
        set => this.RaiseAndSetIfChanged(ref _rightPortId, value);
    }

    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        set => this.RaiseAndSetIfChanged(ref _isButtonEnabled, value);
    }

    public ObservableCollection<Node> AvailableDeviceNodes { get; } = [];

    public Node? SelectedDeviceNode
    {
        get => _selectedDeviceNode;
        set => this.RaiseAndSetIfChanged(ref _selectedDeviceNode, value);
    }

    public void ValidateForm()
    {
        IsButtonEnabled = SelectedDeviceNode is not null &&
                          Name != string.Empty &&
                          Description != string.Empty;
    }

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task AddStreamLoopbackAction()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public async Task CancelAction()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}