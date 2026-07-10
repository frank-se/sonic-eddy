using System;
using SonicEddy.ViewModels.GlobalMasterViewModels;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.ViewModels.MixerOverviewViewModels;

// Window-level VM. Safe to create fresh on every ShowMixerOverviewWindow() call: it holds no
// independent state of its own, only reactive views over the shared LayerA/LayerB/GlobalMaster
// VMs owned by MainWindowViewModel. Dispose() must only tear down the row-list wrappers this
// class created — never the shared VMs (mirrors the GlobalMasterWindow pattern, which never
// disposes its VM on close either, since it too is app-lifetime and shared).
public sealed class MixerOverviewViewModel : IDisposable
{
    public MixerOverviewViewModel(MixerLayerViewModel layerA,
        MixerLayerViewModel layerB, GlobalMasterViewModel globalMaster)
    {
        LayerA = new MixerOverviewLayerViewModel(layerA);
        LayerB = new MixerOverviewLayerViewModel(layerB);
        GlobalMaster = globalMaster;
    }

    public MixerOverviewLayerViewModel LayerA { get; }
    public MixerOverviewLayerViewModel LayerB { get; }
    public GlobalMasterViewModel GlobalMaster { get; }

    public void Dispose()
    {
        LayerA.Dispose();
        LayerB.Dispose();
        // GlobalMaster is deliberately not disposed here — it's owned by MainWindowViewModel.
    }
}
