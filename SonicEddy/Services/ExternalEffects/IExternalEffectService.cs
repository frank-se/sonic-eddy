using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.ExternalEffects;
using SonicEddy.Services.MixerServiceV2;

namespace SonicEddy.Services.ExternalEffects;

public interface IExternalEffectService
{
    IReadOnlyList<ExternalEffectConfig> Effects { get; }
    event Action? Changed;

    Task InitializeAsync();
    Task<ExternalEffectConfig> AddAsync(string name, Node inputNode,
        IReadOnlyList<Port> inputPorts, Node outputNode,
        IReadOnlyList<Port> outputPorts);
    Task UpdateAsync(Guid id, string name, Node inputNode,
        IReadOnlyList<Port> inputPorts, Node outputNode,
        IReadOnlyList<Port> outputPorts);
    Task DeleteAsync(Guid id);
    bool IsAvailable(Guid id);
    string? GetUsedBy(Guid id);
    Task<ExternalEffectInsertProcessor> CreateInsertAsync(Guid id,
        Node insertionInput, Node insertionOutput, string usedBy);
}
