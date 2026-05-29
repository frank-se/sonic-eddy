using System;
using Fr.Sonic.Modules.Models;
using ReactiveUI;

namespace SonicEddy.ViewModels.MonitoringViewModels;

public class MonitoringViewModel : ReactiveObject, IDisposable
{
    public MonitoringViewModel(LoopbackModule? monitoringLoopback)
    {
        if (monitoringLoopback is not null)
            Channel = new MonitoringChannelViewModel(monitoringLoopback);
    }

    public MonitoringChannelViewModel? Channel { get; }

    public void Dispose() => Channel?.Dispose();
}
