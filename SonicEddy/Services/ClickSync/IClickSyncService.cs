using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.ClickSync;

public enum ClickSyncOutput
{
    Click,
    Reset,
    Run
}

public sealed record ClickSyncConverterState(
    Guid Id,
    string Name,
    uint PulsesPerQuarterNote,
    double PulseLengthMs,
    float PulseAmplitude);

public sealed record ClickSyncPortState(
    Port Port,
    bool Selected,
    bool Linked);

public interface IClickSyncService
{
    event Action? Changed;

    bool CanCreate { get; }

    Task InitializeAsync();

    IReadOnlyCollection<ClickSyncConverterState> GetConverters();

    IReadOnlyCollection<ClickSyncPortState> GetPorts(
        Guid converterId,
        ClickSyncOutput output);

    Task AddConverterAsync(string name, uint pulsesPerQuarterNote,
        double pulseLengthMs, float pulseAmplitude);

    Task DeleteConverterAsync(Guid converterId);

    void SetTarget(Guid converterId, ClickSyncOutput output, Port port,
        bool selected);
}
