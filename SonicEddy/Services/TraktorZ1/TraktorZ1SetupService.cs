using System.Collections.Generic;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.TraktorZ1;

public class TraktorZ1SetupService : ITraktorZ1SetupService
{
    private readonly Node?[] _masterFaderNodes = new Node?[2];
    private Node? _xfadeNode;
    private Node? _cueMixNode;

    private readonly List<List<FilterSectionParam>>[] _sectionParams =
        [[], []];

    public void SetMasterFaderNode(TraktorZ1Side side, Node node) =>
        _masterFaderNodes[(int)side] = node;

    public void ClearFilterSections(TraktorZ1Side side) =>
        _sectionParams[(int)side].Clear();

    public void AddFilterParameter(TraktorZ1Side side, int sectionIndex,
        Node pluginNode, string name, float min, float max)
    {
        var sections = _sectionParams[(int)side];
        while (sections.Count <= sectionIndex)
            sections.Add([]);
        sections[sectionIndex].Add(new FilterSectionParam(pluginNode, name, min, max));
    }

    public void SetXfadeNode(Node? node) => _xfadeNode = node;
    public void SetCueMixNode(Node? node) => _cueMixNode = node;

    internal Node? GetMasterFaderNode(TraktorZ1Side side) =>
        _masterFaderNodes[(int)side];

    internal Node? GetXfadeNode() => _xfadeNode;
    internal Node? GetCueMixNode() => _cueMixNode;

    internal IReadOnlyList<IReadOnlyList<FilterSectionParam>> GetSections(
        TraktorZ1Side side) =>
        _sectionParams[(int)side];
}

internal record FilterSectionParam(
    Node PluginNode, string Name, float Min, float Max);
