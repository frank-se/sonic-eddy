using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params;
using Fr.Wireplumber.Model.PropInfo;
using Fr.Wireplumber.Model.Props;

namespace Fr.Wireplumber.Registries.Nodes;

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