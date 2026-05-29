using System;
using Fr.Sonic.Modules.Models;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.ViewModels.MonitoringViewModels;

public class MonitoringChannelViewModel : IDisposable
{
    private readonly PanAndVolumeViewModel _panAndVolume;

    public MonitoringChannelViewModel(LoopbackModule loopbackModule)
    {
        _panAndVolume = new PanAndVolumeViewModel(loopbackModule.PlaybackNode);
    }

    public PanAndVolumeViewModel PanAndVolume => _panAndVolume;

    public void Dispose() => _panAndVolume.Dispose();
}
