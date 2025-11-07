using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using Fr.Wireplumber;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModel;

public class AddStreamDialogViewModel : ViewModelBase
{
    private Stream? _selectedStream;

    public AddStreamDialogViewModel()
    {
        AvailableStreams.AddRange(
            Wireplumber.Nodes.Where(n =>
                    n.Media.Class is "Stream/Output/Audio" or "Audio/Source")
                .Select(n => new Stream(n.Name ?? string.Empty, n.ObjectSerial,
                    1.0,
                    n.Description ?? n.Name ?? string.Empty, null))
        );
    }

    public ObservableCollection<Stream> AvailableStreams { get; set; } = [];
    public bool DialogResult { get; set; }
    public Interaction<Unit, Unit> Close { get; } = new();

    public Stream? SelectedStream
    {
        get => _selectedStream;
        set => this.RaiseAndSetIfChanged(ref _selectedStream, value);
    }

    public async void AddStreamAction()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public async void CancelAction()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }
}