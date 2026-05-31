using System.Collections.Generic;

namespace SonicEddy.Services.TraktorZ1;

public class TraktorZ1SetupService : ITraktorZ1SetupService
{
    private readonly ulong?[] _masterFaderNodes = new ulong?[2];

    private readonly List<List<FilterSectionParam>>[] _sectionParams =
        [[], []];

    public void SetMasterFaderNode(TraktorZ1Side side, ulong objectId) =>
        _masterFaderNodes[(int)side] = objectId;

    public void ClearFilterSections(TraktorZ1Side side) =>
        _sectionParams[(int)side].Clear();

    public void AddFilterParameter(TraktorZ1Side side, int sectionIndex,
        ulong pluginObjectId, string name, float min, float max)
    {
        var sections = _sectionParams[(int)side];
        while (sections.Count <= sectionIndex)
            sections.Add([]);
        sections[sectionIndex].Add(new FilterSectionParam(pluginObjectId, name, min, max));
    }

    internal ulong? GetMasterFaderNode(TraktorZ1Side side) =>
        _masterFaderNodes[(int)side];

    internal IReadOnlyList<IReadOnlyList<FilterSectionParam>> GetSections(
        TraktorZ1Side side) =>
        _sectionParams[(int)side];
}

internal record FilterSectionParam(
    ulong PluginObjectId, string Name, float Min, float Max);
