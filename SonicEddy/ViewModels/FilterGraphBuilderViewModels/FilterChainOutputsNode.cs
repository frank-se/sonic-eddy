using System.Collections.Generic;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class FilterChainOutputsNode : NodeBase
{
    public FilterChainOutputsNode() : base()
    {
        InPorts =
        [
            new FilterGraphInPort("FL", this), new FilterGraphInPort("FR", this)
        ];
    }
}