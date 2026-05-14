using System.Collections.Concurrent;
using Fr.Wireplumber.Factories;
using Fr.Wireplumber.Factories.Implementation;
using Fr.Wireplumber.Model.Messages;
using Fr.Wireplumber.Model.Params;
using Fr.Wireplumber.Model.PropInfo;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules;
using Fr.Wireplumber.PInvoke;
using Fr.Wireplumber.Registries.Clients;
using Fr.Wireplumber.Registries.Devices;
using Fr.Wireplumber.Registries.Links;
using Fr.Wireplumber.Registries.Metadata;
using Fr.Wireplumber.Registries.Modules;
using Fr.Wireplumber.Registries.Nodes;
using Fr.Wireplumber.Registries.Ports;

// ReSharper disable MemberCanBePrivate.Global

namespace Fr.Wireplumber;

/// <summary>
/// Main entry point for the API. Gives access to everything else.
/// </summary>
public static class Wireplumber
{
    /// <summary>
    /// The client registry provides access to all clients.
    /// </summary>
    public static ClientRegistry ClientRegistry { get; } = new();

    /// <summary>
    /// The device registry provides access to all devices.
    /// </summary>
    public static DeviceRegistry DeviceRegistry { get; } = new();

    /// <summary>
    /// The link registry provides access to all links.
    /// </summary>
    public static LinkRegistry LinkRegistry { get; } = new();

    /// <summary>
    /// The node registry provides access to all nodes.
    /// </summary>
    public static NodeRegistry NodeRegistry { get; } = new();

    /// <summary>
    /// The port registry provides access to all ports.
    /// </summary>
    public static PortRegistry PortRegistry { get; } = new();

    /// <summary>
    /// The module registry provides access to all modules created by the
    /// module factory.
    /// </summary>
    public static ModuleRegistry ModuleRegistry { get; } = new();

    /// <summary>
    /// The metadata registry provides access to all the metadata collection
    /// currently known to pipewire.
    /// </summary>
    public static MetadataRegistry MetadataRegistry { get; } =
        new(NodeRegistry);

    // ReSharper disable once InconsistentNaming
    private static readonly ModuleFactory _moduleFactory = new(ModuleRegistry);

    /// <summary>
    /// The module factory allows the creation of pipewire modules.
    /// </summary>
    public static IModuleFactory ModuleFactory => _moduleFactory;

    // ReSharper disable once InconsistentNaming
    private static readonly LinkFactory _linkFactory = new();

    /// <summary>
    /// The link factory creates and deletes links
    /// </summary>
    public static ILinkFactory LinkFactory => _linkFactory;
    
    private static readonly CancellationTokenSource
        ThreadProcessingCancellationTokenSource = new();

    private static readonly CancellationToken
        ThreadProcessingCancellationToken =
            ThreadProcessingCancellationTokenSource.Token;

    private static Thread? _processPipewireUpdatesThread;

    private static bool _running = true;

    private static readonly ConcurrentDictionary<ulong, Properties> Props = [];

    private static readonly ConcurrentDictionary<ulong, ParamsUpdatedMessage>
        Params = [];

    private static readonly ConcurrentDictionary<ulong, PropertyInfoCollection>
        PropertyInfos = [];

    /// <summary>
    /// Start wireplumber and pipewire. This sets up the pipewire,
    /// and wireplumber main loops, and connects to the core. Additionally, all
    /// the thread for data processing are started. Call this before doing
    /// anything else with the API.
    /// </summary>
    public static void Start()
    {
        FrWireplumberLib.Init();
        FrWireplumberLib.Start();
        _processPipewireUpdatesThread = new(ProcessPipewireUpdatesThread);
        _processPipewireUpdatesThread.Start();
    }

    /// <summary>
    /// Stop all threads, and disconnect from pipewire, and wireplumber.
    /// </summary>
    public static void Stop()
    {
        FrWireplumberLib.Stop();
        _running = false;
        ThreadProcessingCancellationTokenSource.Cancel();
    }

    private static void ProcessPipewireUpdatesThread()
    {
        try
        {
            while (_running)
            {
                var message =
                    FrWireplumberLib.UpdatesFromPipewire.Take(
                        ThreadProcessingCancellationToken);

                switch (message)
                {
                    case MetadataUpdateMessageBase metadataUpdate:
                        ProcessMetadataUpdate(metadataUpdate);
                        break;
                    case ParamsUpdatedMessage paramsUpdatedMessage:
                        ProcessParamsUpdate(paramsUpdatedMessage);
                        break;
                    case PropInfosUpdatedMessage propertyInfosUpdatedMessage:
                        ProcessPipewirePropertyInfoAddedMessage(
                            propertyInfosUpdatedMessage.propInfos);
                        break;
                    case PropsChangesMessage propsChangesMessage:
                        ProcessPipewirePropsUpdatedMessage(propsChangesMessage
                            .props);
                        break;
                    case IWireplumberObjectAddedMessages objectAddedMessage:
                        ProcessPipewireObjectAddedMessage(objectAddedMessage);
                        break;
                    case ObjectDeletedMessage deletedMessage:
                        ProcessObjectDeletedMessage(deletedMessage);
                        break;
                    case PropsEnumFailedMessage propsEnumFailedMessage:
                        ProcessPipewirePropsEnumFailedMessage(
                            propsEnumFailedMessage);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when
            (ThreadProcessingCancellationTokenSource.IsCancellationRequested)
        {
        }
    }

    private static void ProcessPipewirePropsEnumFailedMessage(
        PropsEnumFailedMessage message)
    {
        NodeRegistry.FailParams(message.ObjectSerial);
        NodeRegistry.FailProperties(message.ObjectSerial);
    }

    private static void ProcessPipewirePropsUpdatedMessage(
        Properties properties)
    {
        var objectSerial = properties.ObjectSerial;
        Props.AddOrUpdate(objectSerial, properties,
            (_, _) => properties);

        NodeRegistry.UpdateProperties(properties);
    }

    private static void ProcessPipewirePropertyInfoAddedMessage(
        PropertyInfoCollection propertyInfos)
    {
        var objectSerial = propertyInfos.ObjectSerial;
        PropertyInfos.TryAdd(objectSerial, propertyInfos);

        NodeRegistry.UpdatePropertyInfos(propertyInfos);
    }

    private static void ProcessParamsUpdate(
        ParamsUpdatedMessage paramUpdate)
    {
        var objectSerial = paramUpdate.ObjectSerial;
        var mergedParams = Params.AddOrUpdate(objectSerial,
            paramUpdate, (_, old) =>
            {
                var merged =
                    new Dictionary<string, IParameter>(old.Parameters);
                foreach (var keyAndValue in paramUpdate.Parameters)
                {
                    merged[keyAndValue.Key] = keyAndValue.Value;
                }

                return new(objectSerial, merged);
            });

        NodeRegistry.UpdateParams(mergedParams);
    }

    private static void ProcessMetadataUpdate(
        MetadataUpdateMessageBase metadataUpdate)
    {
        switch (metadataUpdate)
        {
            case MetadataAddedMessage metadataAddedMessage:
                MetadataRegistry.Add(metadataAddedMessage.MetadataName);
                break;
            case MetadataEntryUpdatedMessage metadataEntryUpdatedMessage
                :
                MetadataRegistry.AddOrUpdateMetadataEntry(new(
                    metadataEntryUpdatedMessage.MetadataName,
                    metadataEntryUpdatedMessage.Subject,
                    metadataEntryUpdatedMessage.Key,
                    metadataEntryUpdatedMessage.Type,
                    metadataEntryUpdatedMessage.Value));
                break;
            case MetadataEntryDeletedMessage metadataEntryDeletedMessage
                :
                MetadataRegistry.DeleteMetadataEntry(
                    metadataEntryDeletedMessage.MetadataName,
                    metadataEntryDeletedMessage.Subject,
                    metadataEntryDeletedMessage.Key);
                break;
        }
    }

    private static void ProcessPipewireObjectAddedMessage(
        IWireplumberObjectAddedMessages data)
    {
        switch (data)
        {
            case WireplumberDeviceAddedMessage deviceMessage:
                DeviceRegistry.Add(deviceMessage.Device,
                    deviceMessage.TaskCompletionSources);

                var nodes =
                    NodeRegistry.FindByDeviceId(deviceMessage.Device
                        .ObjectId);

                if (nodes.Count != 0)
                {
                    DeviceRegistry.UpdateNodesList(
                        deviceMessage.Device.ObjectSerial, nodes);

                    NodeRegistry.UpdateDevices(
                        nodes.Select(node => node.ObjectSerial),
                        deviceMessage.Device);
                }

                break;
            case WireplumberNodeAddedMessage nodeMessage:
                var collection = MetadataRegistry.GetByName("default");
                var applicableMetadata =
                    collection?.MetadataEntries.Where(pair =>
                        pair.Subject == nodeMessage.Node.ObjectId);
                foreach (var metadataEntry in applicableMetadata ?? [])
                {
                    nodeMessage.Node.Metadata
                        .AddOrUpdate(metadataEntry);
                }

                NodeRegistry.Add(nodeMessage.Node,
                    nodeMessage.TaskCompletionSources);
                _moduleFactory.UpdateNodesForWaitingModules(nodeMessage
                    .Node);

                var objectSerial = nodeMessage.Node.ObjectSerial;
                if (PropertyInfos.TryGetValue(
                        objectSerial,
                        out var propInfos))
                {
                    NodeRegistry.UpdatePropertyInfos(propInfos);
                }

                if (Params.TryGetValue(objectSerial,
                        out var parameters))
                {
                    NodeRegistry.UpdateParams(parameters);
                }

                if (Props.TryGetValue(objectSerial,
                        out var properties))
                {
                    NodeRegistry.UpdateProperties(properties);
                }

                if (nodeMessage.Node.DeviceAssignment is not null)
                {
                    DeviceRegistry.AddToNodeListByObjectId(
                        nodeMessage.Node.DeviceAssignment.Id,
                        nodeMessage.Node);

                    var device =
                        DeviceRegistry.GetByObjectId(nodeMessage.Node
                            .DeviceAssignment.Id);

                    if (device is not null)
                        NodeRegistry.UpdateDevices(
                            [nodeMessage.Node.ObjectSerial], device);
                }

                break;
            case WireplumberLinkAddedMessage linkMessage:
                LinkRegistry.Add(linkMessage.Link, new());
                break;
            case WireplumberPortAddedMessage portMessage:
                PortRegistry.Add(portMessage.Port, new());
                break;
            case WireplumberClientAddedMessage clientMessage:
                ClientRegistry.Add(clientMessage.Client, new());
                break;
        }
    }

    private static void ProcessObjectDeletedMessage(
        ObjectDeletedMessage deletedObject)
    {
        switch (deletedObject.ObjectType)
        {
            case wireplumber_object_type.node:
                NodeRegistry.Delete(deletedObject.ObjectSerial);
                break;
            case wireplumber_object_type.port:
                PortRegistry.Delete(deletedObject.ObjectSerial);
                break;
            case wireplumber_object_type.link:
                LinkRegistry.Delete(deletedObject.ObjectSerial);
                break;
            case wireplumber_object_type.device:
                DeviceRegistry.Delete(deletedObject.ObjectSerial);
                break;
            case wireplumber_object_type.client:
                ClientRegistry.Delete(deletedObject.ObjectSerial);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
