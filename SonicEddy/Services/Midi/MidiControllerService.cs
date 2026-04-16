using System;
using Fr.Pw.Midi;
using Fr.Pw.Midi.PInvoke;
using Microsoft.Extensions.Logging;

namespace SonicEddy.Services.Midi;

public class MidiControllerService : IMidiControllerService, IDisposable
{
    public MidiControllerService(ILogger<MidiControllerService> logger)
    {
        _logger = logger;
        FrPwMidi.LayerChanged += OnLayerChanged;
        FrPwMidi.SelectedChannelChanged += OnSelectedChannelChanged;
        FrPwMidi.DialSelectionModeChanged += OnDialSelectionModeSelect;
        FrPwMidi.SelectedFilterParamsSectionChanged +=
            OnSelectedFilterParamsSectionChanged;

        FrPwMidi.FilterParamsSectionMovedRight +=
            OnFilterParamsSectionMoveRight;

        FrPwMidi.FilterParamsSectionMovedLeft += OnFilterParamsSectionMoveLeft;

        FrPwMidi.MidiControlChangeUpdate += OnMidiControlChangeUpdate;
    }

    private readonly ILogger<MidiControllerService> _logger;

    public void SetSelectedPluginPage(ulong pluginId, ulong pageNumber) =>
        FrPwMidi.SetSelectedPluginPage(pluginId, pageNumber);

    public void ClearSelectedChannel() => FrPwMidi.ClearSelectedChannel();

    public void SetSelectedChannel(ChannelType channelType, ulong channelId) =>
        FrPwMidi.SetSelectedChannel(channelType, channelId);

    public void SetSelectedLayer(ulong layerId) =>
        FrPwMidi.SetSelectedLayer(layerId);

    public event Action<LayerSelectEventArgs>? LayerChanged;

    public event Action<ChannelSelectEventArgs>? SelectedChannelChanged;

    public event Action<DialSelectionModeSelectEventArgs>?
        DialSelectionModeChanged;

    public event Action<FilterParamsSectionSelectEventArgs>?
        SelectedFilterParamsSectionChanged;

    public event Action<FilterParamsSectionMovePagesEventArgs>?
        FilterParamsSectionMovedRight;

    public event Action<FilterParamsSectionMovePagesEventArgs>?
        FilterParamsSectionMovedLeft;

    public event Action<MidiControlChangeUpdateEventArgs>?
        MidiControlChangeUpdate;

    private void OnLayerChanged(LayerSelectEventArgs eventArgs) =>
        LayerChanged?.Invoke(eventArgs);

    private void OnSelectedChannelChanged(ChannelSelectEventArgs eventArgs) =>
        SelectedChannelChanged?.Invoke(eventArgs);

    private void OnDialSelectionModeSelect(
        DialSelectionModeSelectEventArgs eventArgs) =>
        DialSelectionModeChanged?.Invoke(eventArgs);

    private void OnSelectedFilterParamsSectionChanged(
        FilterParamsSectionSelectEventArgs eventArgs) =>
        SelectedFilterParamsSectionChanged?.Invoke(eventArgs);

    private void OnFilterParamsSectionMoveRight(
        FilterParamsSectionMovePagesEventArgs eventArgs) =>
        FilterParamsSectionMovedRight?.Invoke(eventArgs);

    private void OnFilterParamsSectionMoveLeft(
        FilterParamsSectionMovePagesEventArgs eventArgs) =>
        FilterParamsSectionMovedLeft?.Invoke(eventArgs);

    private void OnMidiControlChangeUpdate(
        MidiControlChangeUpdateEventArgs eventArgs)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Midi ControlChangeUpdate: {eventArgs}",
                eventArgs);

        MidiControlChangeUpdate?.Invoke(eventArgs);
    }

    public void Dispose()
    {
        FrPwMidi.LayerChanged -= OnLayerChanged;
        FrPwMidi.SelectedChannelChanged -= OnSelectedChannelChanged;
        FrPwMidi.DialSelectionModeChanged -= OnDialSelectionModeSelect;
        FrPwMidi.SelectedFilterParamsSectionChanged -=
            OnSelectedFilterParamsSectionChanged;

        FrPwMidi.FilterParamsSectionMovedRight -=
            OnFilterParamsSectionMoveRight;

        FrPwMidi.FilterParamsSectionMovedLeft -= OnFilterParamsSectionMoveLeft;

        FrPwMidi.MidiControlChangeUpdate -= OnMidiControlChangeUpdate;

        GC.SuppressFinalize(this);
    }
}