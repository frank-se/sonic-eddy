using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using Fr.Wireplumber;
using ReactiveUI;
using SonicEddy.Audio;

namespace SonicEddy.ViewModels.MixerViewModels;

public class AddStreamDialogViewModel : ViewModelBase
{
    private Stream? _selectedStream;

    public AddStreamDialogViewModel()
    {
        var nodes =
            Wireplumber.Nodes.Where(n =>
                    n.Media.Class is "Stream/Output/Audio")
                .ToList();

        var props = nodes.Select(n =>
            {
                return Wireplumber.Props.FirstOrDefault(p =>
                    p.ObjectSerial == n.ObjectSerial);
            })
            .ToList();

        var streams = nodes.Zip(props)
            .Select(pair =>
            {
                var (node, prop) = pair;
                var isStereo = prop?.Channels.Count == 2;

                var pan = 0.0;
                if (isStereo)
                {
                    var leftGain = prop!.Channels.First()
                        .Volume;
                    var rightGain = prop!.Channels.Last()
                        .Volume;
                    pan = Pan.GetPanFromGains(leftGain, rightGain);
                }

                return new Stream(node.Name ?? string.Empty, node.ObjectSerial,
                    prop?.Channels.FirstOrDefault()
                        ?
                        .Volume ?? 0,
                    node.Description ?? node.Name ?? string.Empty, null, pan,
                    isStereo, node.ObjectId);
            });

        AvailableStreams.AddRange(streams);
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