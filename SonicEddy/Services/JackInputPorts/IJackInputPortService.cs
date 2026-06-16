using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SonicEddy.Services.JackInputPorts;

public interface IJackInputPortService
{
    List<JackInputPort> JackInputPorts { get; }

    Task InitializeAsync();

    Task AddJackInputPort(string name, string clientName, string[] jackPortNames);

    Task DeleteJackInputPort(JackInputPort port);

    event Action<JackInputPort>? Added;
    event Action<JackInputPort>? Deleted;
}
