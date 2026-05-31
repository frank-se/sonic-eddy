namespace SonicEddy.Services.TraktorZ1;

public interface ITraktorZ1SetupService
{
    void SetMasterFaderNode(TraktorZ1Side side, ulong objectId);

    void ClearFilterSections(TraktorZ1Side side);

    void AddFilterParameter(TraktorZ1Side side, int sectionIndex,
        ulong pluginObjectId, string name, float min, float max);
}
