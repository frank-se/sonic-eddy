using System;
using Fr.Pw.Midi;
using Fr.Pw.Midi.PInvoke;

namespace SonicEddy.Services.Midi;

public interface IMidiControllerService
{
    void SetSelectedPluginPage(ulong pluginId, ulong pageNumber);

    void ClearSelectedChannel();
    
    void SetSelectedChannel(ChannelType channelType, ulong channelId);

    void SetSelectedLayer(ulong layerId);

    event Action<LayerSelectEventArgs>? LayerChanged;
    event Action<ChannelSelectEventArgs>? SelectedChannelChanged;

    event Action<DialSelectionModeSelectEventArgs>? DialSelectionModeChanged;

    event Action<FilterParamsSectionSelectEventArgs>?
        SelectedFilterParamsSectionChanged;

    event Action<FilterParamsSectionMovePagesEventArgs>?
        FilterParamsSectionMovedRight;

    event Action<FilterParamsSectionMovePagesEventArgs>?
        FilterParamsSectionMovedLeft;
}