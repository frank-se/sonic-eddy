using Fr.Pw.Midi.PInvoke;
using Fr.Wireplumber.Model.Objects;

namespace Fr.Pw.Midi.MidiPorts;

public record MidiPort(
    ulong Id,
    Node Sender,
    Node Receiver)
{
    internal void TriggerLayerSelectedEvent(ulong layerId) =>
        LayerSelected?.Invoke(layerId);

    internal void TriggerChannelSelectedEvent(ulong channelId) =>
        ChannelSelected?.Invoke(channelId);

    internal void TriggerDialSelectionModeChangedEvent(ulong channelId,
        DialMode mode) => DialSelectionModeChanged?.Invoke(channelId, mode);

    internal void TriggerFilterParamsSectionChangedEvent(ulong channelId,
        ulong sectionId) =>
        FilterParamsSectionChanged?.Invoke(channelId, sectionId);

    public event Action<ulong>? LayerSelected;
    public event Action<ulong>? ChannelSelected;
    public event Action<ulong, DialMode>? DialSelectionModeChanged;
    public event Action<ulong, ulong>? FilterParamsSectionChanged;
}