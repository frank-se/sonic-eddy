using Fr.Sonic.Model.Config.FilterChain;
using Fr.Sonic.Model.Config.LoopbackModule;
using Fr.Sonic.Modules.Models;

namespace Fr.Sonic.Factories;

/// <summary>
/// The module factory creates pipewire modules. It takes the config, and sends
/// it along to the pipewire module factory. It then asynchronously waits until
/// the module, and the respective nodes are created.
/// </summary>
public interface IModuleFactory
{
    /// <summary>
    /// Create a loopback module.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="config">The loopback module config</param>
    /// <returns></returns>
    Task<LoopbackModule> CreateLoopbackModuleAsync(string name,
        LoopbackModuleConfig config);

    /// <summary>
    /// Create a filter chain module
    /// </summary>
    /// <param name="name"></param>
    /// <param name="config">The filter chain config</param>
    /// <returns></returns>
    Task<FilterChain> CreateFilterChainAsync(string name,
        FilterChainModuleConfig config);
}