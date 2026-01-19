using System;
using System.Net.Security;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace SonicEddy.ViewModels.SaveFilterGraphDialogViewModels;

public class SaveFilterGraphDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public SaveFilterGraphDialogViewModel()
    {
        this.WhenAnyValue(x => x.Name)
            .Subscribe(_ => CheckIfSaveIsEnabled())
            .DisposeWith(_disposables);
    }

    private void CheckIfSaveIsEnabled()
    {
        IsButtonEnabled = Name != string.Empty;
    }

    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private bool _isButtonEnabled;

    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        set => this.RaiseAndSetIfChanged(ref _isButtonEnabled, value);
    }

    public bool DialogResult = false;

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