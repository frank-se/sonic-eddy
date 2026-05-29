using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace SonicEddy.ViewModels.SavePresetDialogViewModels;

public class SavePresetDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public SavePresetDialogViewModel()
    {
        this.WhenAnyValue(x => x.Name)
            .Subscribe(_ => IsButtonEnabled = Name != string.Empty)
            .DisposeWith(_disposables);
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool DialogResult;

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task Cancel()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task FinishSave()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public void Dispose() => _disposables.Dispose();
}
