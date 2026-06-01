using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.TraktorZ1;

public interface ITraktorZ1SetupService
{
    void SetMasterFaderNode(TraktorZ1Side side, Node node);

    void ClearFilterSections(TraktorZ1Side side);

    void AddFilterParameter(TraktorZ1Side side, int sectionIndex,
        Node pluginNode, string name, float min, float max);

    void SetXfadeNode(Node? node);
    void SetCueMixNode(Node? node);
}
