using System.Collections.Generic;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class FilterChainInputsNode : NodeBase
{
    public FilterChainInputsNode() : base()
    {
        OutPorts =
        [
            new FilterGraphInPort("FL", this), new FilterGraphInPort("FR", this)
        ];
    }
}