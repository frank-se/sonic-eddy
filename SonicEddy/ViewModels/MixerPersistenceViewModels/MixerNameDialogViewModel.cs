using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerPersistenceViewModels;

public sealed class MixerNameDialogViewModel : ViewModelBase
{
    public MixerNameDialogViewModel(string name = "")
    {
        Name = name;
        this.WhenAnyValue(viewModel => viewModel.Name)
            .Subscribe(value => IsButtonEnabled =
                !string.IsNullOrWhiteSpace(value));
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsButtonEnabled
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool DialogResult { get; private set; }
    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task Cancel()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task Save()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }
}
