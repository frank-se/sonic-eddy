using System;
using System.Collections.ObjectModel;
using System.Linq;
using Fr.Pw.Midi;
using Fr.Wireplumber.Registries.Nodes;
using Microsoft.Extensions.Logging;
using SonicEddy.Services.Midi;

namespace SonicEddy.ViewModels.MidiParameterChangeMonitorViewModels;

public class MidiParameterChangeMonitorViewModel : ViewModelBase, IDisposable
{
    private const int MaxTrackedParameters = 10;

    public MidiParameterChangeMonitorViewModel(
        ILogger<MidiParameterChangeMonitorViewModel> logger,
        IMidiControllerService midiControllerService, NodeRegistry nodeRegistry)
    {
        _logger = logger;
        _midiControllerService = midiControllerService;
        _nodeRegistry = nodeRegistry;

        _midiControllerService.MidiControlChangeUpdate +=
            OnMidiParameterControlChangeUpdate;
    }

    private void OnMidiParameterControlChangeUpdate(
        MidiControlChangeUpdateEventArgs eventArgs) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            ProcessMidiParameterChangeUpdate(eventArgs));

    private void ProcessMidiParameterChangeUpdate(
        MidiControlChangeUpdateEventArgs eventArgs)
    {
        var found = false;

        foreach (var midiParameter in MidiParameters)
        {
            if (midiParameter.ObjectId != eventArgs.ObjectId ||
                midiParameter.Name != eventArgs.ParameterName) continue;

            midiParameter.CurrentValue = eventArgs.NormalizedKnownValue;
            midiParameter.TargetValue = eventArgs.NormalizedControllerValue;
            midiParameter.CatchingUp = eventArgs.CatchingUp;
            midiParameter.UpdateTime = DateTime.UtcNow;
            found = true;
            break;
        }

        if (!found)
        {
            var node = _nodeRegistry.GetByObjectId(eventArgs.ObjectId);

            MidiParameters.Add(new(eventArgs.ObjectId,
                node?.Name ?? string.Empty,
                eventArgs.ParameterName, eventArgs.NormalizedKnownValue,
                eventArgs.NormalizedControllerValue, eventArgs.CatchingUp));
        }

        if (MidiParameters.Count <= MaxTrackedParameters) return;

        while (MidiParameters.Count < MaxTrackedParameters)
        {
            var leastRecentlyUpdated = MidiParameters.MinBy(p => p.UpdateTime);

            if (leastRecentlyUpdated == null)
            {
                _logger.LogWarning(
                    "Too many parameters in collection, but cannot find parameter to remove!");

                break;
            }

            MidiParameters.Remove(leastRecentlyUpdated);
        }
    }

    private readonly ILogger<MidiParameterChangeMonitorViewModel> _logger;
    private readonly IMidiControllerService _midiControllerService;
    private readonly NodeRegistry _nodeRegistry;

    public ObservableCollection<MidiParameterViewModel> MidiParameters
    {
        get;
        set;
    } = [];

    public void Dispose()
    {
        _midiControllerService.MidiControlChangeUpdate -=
            OnMidiParameterControlChangeUpdate;

        GC.SuppressFinalize(this);
    }
}