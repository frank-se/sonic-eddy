using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;
using ReactiveUI;

namespace SonicEddy.ViewModels.RecordingPickUpViewModels;

public class AddRecordingPickUpDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposable = new();

    public AddRecordingPickUpDialogViewModel(
        ObservableCollection<Node> sourceNodes,
        ObservableCollection<Node> destNodes)
    {
        SourceNodes = sourceNodes;
        DestNodes = destNodes;

        this.WhenAnyValue(x => x.Name)
            .Subscribe(_ => IsButtonEnabled = IsValid)
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedSourceNode)
            .Subscribe(_ => IsButtonEnabled = IsValid)
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedDestNode)
            .Subscribe(_ => IsButtonEnabled = IsValid)
            .DisposeWith(_disposable);
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<Node> SourceNodes { get; }
    public ObservableCollection<Node> DestNodes { get; }

    public Node? SelectedSourceNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Node? SelectedDestNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsValid =>
        Name != string.Empty &&
        SelectedSourceNode is not null &&
        SelectedDestNode is not null;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

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

    public void Dispose()
    {
        _disposable.Dispose();
        GC.SuppressFinalize(this);
    }
}
