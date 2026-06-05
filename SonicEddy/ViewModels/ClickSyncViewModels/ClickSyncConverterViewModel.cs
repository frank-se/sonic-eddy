using System;
using SonicEddy.Services.ClickSync;

namespace SonicEddy.ViewModels.ClickSyncViewModels;

public sealed class ClickSyncConverterViewModel(
    ClickSyncConverterState state) : ViewModelBase
{
    public Guid Id => state.Id;
    public string Name => state.Name;
    public string Details =>
        $"{state.PulsesPerQuarterNote} PPQN, {state.PulseLengthMs:0.0##} ms, {state.PulseAmplitude:0.##}";
}
