using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using SonicEddy.Contracts.Mixers;

namespace SonicEddy.ViewModels.MixerPersistenceViewModels;

public sealed class LoadMixerDialogViewModel : ViewModelBase
{
    public LoadMixerDialogViewModel(IEnumerable<Mixer> mixers)
    {
        Mixers = new(mixers.OrderBy(mixer => mixer.Name));
    }

    public ObservableCollection<Mixer> Mixers { get; }

    public Mixer? SelectedMixer
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            IsButtonEnabled = value is not null;
        }
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

    public async Task Load()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }
}
