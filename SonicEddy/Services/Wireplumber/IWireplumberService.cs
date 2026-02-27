using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Config.LoopbackModule;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;

namespace SonicEddy.Services.Wireplumber;

public interface IWireplumberService
{
    List<Port> GetPortsForNode(Node node);
    
    List<Node> GetTargetObjectsForCaptureNode();
    List<Node> GetTargetObjectsForPlaybackNode();
    
    List<Node> GetPlaybackNodes();
    List<Node> GetCaptureNodes();

    bool IsPlaybackNode(Node node);
    bool IsCaptureNode(Node node);
    
    List<Port> GetMidiPorts();

    Task<LoopbackModule> CreateLoopbackModule(string name,
        LoopbackModuleConfig config);
    
    event Action<Node>? NodeAdded;
    event Action<Node>? NodeDeleted;
}