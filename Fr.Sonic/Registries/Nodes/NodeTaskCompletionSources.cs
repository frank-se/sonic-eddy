using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using Fr.Sonic.Model.PropInfo;
using Fr.Sonic.Model.Props;

namespace Fr.Sonic.Registries.Nodes;

/// <summary>
/// Task completion sources for nodes data
/// </summary>
/// <param name="InitialPropertyInfoCompletionSource"></param>
/// <param name="InitialParamsTaskCompletionSource"></param>
/// <param name="InitialPropertiesTaskCompletionSource"></param>
/// <param name="DeviceTaskCompletionSource"></param>
public record NodeTaskCompletionSources(
    TaskCompletionSource<PropertyInfoCollection>
        InitialPropertyInfoCompletionSource,
    TaskCompletionSource<Dictionary<string, IParameter>?>
        InitialParamsTaskCompletionSource,
    TaskCompletionSource<Properties?> InitialPropertiesTaskCompletionSource,
    TaskCompletionSource<Device> DeviceTaskCompletionSource)
{
    internal NodeTaskCompletionSources() : this(new(), new(), new(), new())
    {
    }
}