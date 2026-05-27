using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;

public class AvailableBuiltinNode : ReactiveObject
{
    private decimal _channelCount = 2;

    public AvailableBuiltinNode(BuiltinNodeType type)
    {
        var def = BuiltinNodeCatalog.Get(type);
        NodeType = type;
        DisplayName = def.DisplayName;
        HasVariableChannelCount = def.HasVariableChannelCount;
    }

    public BuiltinNodeType NodeType { get; }
    public string DisplayName { get; }
    public bool HasVariableChannelCount { get; }

    public decimal ChannelCount
    {
        get => _channelCount;
        set => this.RaiseAndSetIfChanged(ref _channelCount,
            value < 1 ? 1 : value > 8 ? 8 : value);
    }
}
