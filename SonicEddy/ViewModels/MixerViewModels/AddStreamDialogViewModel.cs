using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using Fr.Wireplumber;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Audio;

namespace SonicEddy.ViewModels.MixerViewModels;

public class AddStreamDialogViewModel : ViewModelBase
{
    private StreamViewModel? _selectedStream;

    public AddStreamDialogViewModel(
        ObservableCollection<TargetObject> availableTargetObjects,
        ReactiveCommand<StreamViewModel, Unit> removeStreamCommand)
    {
        List<Node> nodes = [];
        nodes.AddRange(
            Wireplumber.NodeRegistry.Objects.Where(n =>
                n.Media.Class is "Stream/Output/Audio" &&
                n.Properties.IsCompleted));

        nodes.AddRange(Wireplumber.NodeRegistry.Objects.Where(node =>
            node.Media.Class is "Audio/Source"));

        var streams = nodes.Select(node =>
        {
            var isStereo = node.Properties.Result.Channels.Count == 2;

            var pan = 0.0;
            if (isStereo)
            {
                var leftGain = node.Properties.Result.Channels.First()
                    .Volume;
                var rightGain = node.Properties.Result.Channels.Last()
                    .Volume;
                pan = Pan.GetPanFromGains(leftGain, rightGain);
            }

            return new StreamViewModel(node.Name ?? string.Empty,
                node.ObjectSerial,
                node.Properties.Result.Channels.FirstOrDefault()
                    ?
                    .Volume ?? 0,
                node.Description ?? node.Name ?? string.Empty, null, pan,
                isStereo, node.ObjectId, availableTargetObjects,
                removeStreamCommand);
        });

        AvailableStreams.AddRange(streams);
    }

    public ObservableCollection<StreamViewModel>
        AvailableStreams { get; set; } = [];

    public bool DialogResult { get; set; }
    public Interaction<Unit, Unit> Close { get; } = new();

    public StreamViewModel? SelectedStream
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