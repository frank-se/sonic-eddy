using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.CameraRouter;

public interface ICameraRouterService
{
    IReadOnlyList<CameraSlot> Slots { get; }

    event Action? SlotsChanged;

    Task InitializeAsync();

    Task AssignSlotAsync(int slotIndex, string? sourceNodeName);

    IReadOnlyList<Node> GetCandidateSources();
}
