using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Config.LoopbackModule;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

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