using Fr.Wireplumber.Model.Messages;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params;
using Fr.Wireplumber.Model.PropInfo;
using Fr.Wireplumber.Model.Props;

namespace Fr.Wireplumber.Registries.Nodes;

/// <summary>
/// Provides access to pipewire nodes
/// </summary>
public class
    NodeRegistry : Registry<Node, NodeTaskCompletionSources, NodeChangeType>
{
    internal void FailParams(ulong objectSerial) =>
        UpdateByObjectSerial(objectSerial,
            old =>
            {
                old.Params =
                    Task.FromResult<Dictionary<string, IParameter>?>(null);
                return old;
            },
            tcs => tcs.InitialParamsTaskCompletionSource.TrySetResult(null),
            node => node.TriggerParamsChangedEvent(null),
            NodeChangeType.Params);

    internal void UpdateParams(ParamsUpdatedMessage paramsUpdate) =>
        UpdateByObjectSerial(paramsUpdate.ObjectSerial,
            old =>
            {
                old.Params =
                    Task.FromResult<Dictionary<string, IParameter>?>(
                        paramsUpdate
                            .Parameters);
                return old;
            },
            tcs => tcs.InitialParamsTaskCompletionSource.TrySetResult(
                paramsUpdate.Parameters),
            node => node.TriggerParamsChangedEvent(paramsUpdate.Parameters),
            NodeChangeType.Params);

    internal void FailProperties(ulong objectSerial) =>
        UpdateByObjectSerial(objectSerial,
            old =>
            {
                old.Properties = Task.FromResult<Properties?>(null);
                return old;
            },
            tcs => tcs.InitialPropertiesTaskCompletionSource.TrySetResult(null),
            node => node.TriggerPropertiesChangedEvent(null),
            NodeChangeType.Properties);

    internal void UpdateProperties(Properties properties) =>
        UpdateByObjectSerial(properties.ObjectSerial,
            old =>
            {
                old.Properties = Task.FromResult<Properties?>(properties);
                return old;
            },
            tcs =>
                tcs.InitialPropertiesTaskCompletionSource.TrySetResult(
                    properties),
            node => node.TriggerPropertiesChangedEvent(properties),
            NodeChangeType.Properties);

    internal void UpdatePropertyInfos(
        PropertyInfoCollection propertyInfoCollection) =>
        UpdateOnce(propertyInfoCollection.ObjectSerial,
            tcs => tcs.InitialPropertyInfoCompletionSource.TrySetResult(
                propertyInfoCollection));

    internal void UpdateDevices(IEnumerable<ulong> objectSerials, Device device)
    {
        foreach (var objectSerial in objectSerials)
        {
            UpdateOnce(objectSerial,
                tcs => tcs.DeviceTaskCompletionSource.TrySetResult(device));
        }
    }

    internal List<Node> FindByDeviceId(ulong deviceId) =>
        Objects.Where(n => n.DeviceAssignment?.Id == deviceId).ToList();
}