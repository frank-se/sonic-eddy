using System.Collections.Concurrent;
using Fr.Sonic.Factories;
using Fr.Sonic.Factories.Implementation;
using Fr.Sonic.Model.Messages;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using Fr.Sonic.Model.PropInfo;
using Fr.Sonic.Model.Props;
using Fr.Sonic.Monitoring;
using Fr.Sonic.MidiPorts;
using Fr.Sonic.MidiPorts.Implementation;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Registries.Clients;
using Fr.Sonic.Registries.Devices;
using Fr.Sonic.Registries.Links;
using Fr.Sonic.Registries.Metadata;
using Fr.Sonic.Registries.Modules;
using Fr.Sonic.Registries.Nodes;
using Fr.Sonic.Registries.Ports;

namespace Fr.Sonic;

public static class FrSonic
{
    /* ── registries ─────────────────────────────────────────────────────── */

    public static ClientRegistry   ClientRegistry   { get; } = new();
    public static DeviceRegistry   DeviceRegistry   { get; } = new();
    public static LinkRegistry     LinkRegistry     { get; } = new();
    public static NodeRegistry     NodeRegistry     { get; } = new();
    public static PortRegistry     PortRegistry     { get; } = new();
    public static ModuleRegistry   ModuleRegistry   { get; } = new();
    public static MetadataRegistry MetadataRegistry { get; } = new(NodeRegistry);

    private static readonly ModuleFactory _moduleFactory = new(ModuleRegistry);
    public static IModuleFactory ModuleFactory => _moduleFactory;

    private static readonly LooperFactory _looperFactory = new(ModuleRegistry);
    public static ILooperFactory LooperFactory => _looperFactory;

    private static readonly LinkFactory _linkFactory = new();
    public static ILinkFactory LinkFactory => _linkFactory;

    /* ── silence producers ──────────────────────────────────────────────── */

    public static IntPtr CreateSilenceProducer(ulong targetSerial) =>
        FrSonicLib.CreateSilenceProducerC(targetSerial);

    public static void DestroySilenceProducer(IntPtr handle) =>
        FrSonicLib.DestroySilenceProducerC(handle);

    /* ── monitoring ─────────────────────────────────────────────────────── */

    private static readonly Monitoring.Monitor _monitor = new();
    public static IMonitor Monitor => _monitor;

    /* ── midi ───────────────────────────────────────────────────────────── */

    public static IMidiPortRegistry? MidiPortRegistry { get; private set; }
    public static IMidiPortFactory?  MidiPortFactory  { get; private set; }

    /* ── internal message queue ─────────────────────────────────────────── */

    internal static readonly BlockingCollection<IMessage> UpdatesFromPipewire = new();

    /* ── processing thread ──────────────────────────────────────────────── */

    private static readonly CancellationTokenSource _cts = new();
    private static Thread? _processingThread;
    private static bool _running = true;

    private static readonly ConcurrentDictionary<ulong, Properties> _props = [];
    private static readonly ConcurrentDictionary<ulong, ParamsUpdatedMessage> _params = [];
    private static readonly ConcurrentDictionary<ulong, PropertyInfoCollection> _propInfos = [];

    /* ── pinned delegates (prevent GC while native callbacks are registered) */

    private static readonly NodeAddedCallback            _cbNodeAdded            = FrSonicWireplumber.OnNodeAdded;
    private static readonly PropsChangedCallback         _cbPropsChanged         = FrSonicWireplumber.OnPropsChanged;
    private static readonly PropsEnumFailedCallback      _cbPropsEnumFailed      = FrSonicWireplumber.OnPropsEnumFailed;
    private static readonly PropInfoAddedCallback        _cbPropInfoAdded        = FrSonicWireplumber.OnPropInfoAdded;
    private static readonly ObjectDeletedCallback        _cbObjectDeleted        = FrSonicWireplumber.OnObjectDeleted;
    private static readonly MetadataAddedCallback        _cbMetadataAdded        = FrSonicWireplumber.OnMetadataAdded;
    private static readonly MetadataEntryUpdatedCallback _cbMetadataEntryUpdated = FrSonicWireplumber.OnMetadataEntryUpdated;
    private static readonly MetadataEntryDeletedCallback _cbMetadataEntryDeleted = FrSonicWireplumber.OnMetadataEntryDeleted;
    private static readonly PeakCallback                 _cbPeak                 = FrSonicMonitoring.OnPeak;
    private static readonly MidiCcUpdateCallback         _cbMidiCcUpdate         = FrSonicMidi.OnMidiCcUpdate;

    /* ── lifecycle ───────────────────────────────────────────────────────── */

    public static void Init(TimeSpan peakUpdateInterval)
    {
        FrSonicLib.InitC(
            _cbNodeAdded,
            _cbPropsChanged,
            _cbPropsEnumFailed,
            _cbPropInfoAdded,
            _cbObjectDeleted,
            _cbMetadataAdded,
            _cbMetadataEntryUpdated,
            _cbMetadataEntryDeleted,
            _cbPeak,
            (ulong)peakUpdateInterval.TotalMilliseconds,
            _cbMidiCcUpdate);
    }

    public static void Start()
    {
        FrSonicLib.StartC();
        _monitor.StartProcessing();

        var midiPortRegistry = new MidiPortRegistry();
        MidiPortRegistry = midiPortRegistry;
        MidiPortFactory  = new MidiPortFactory(NodeRegistry, midiPortRegistry,
            _linkFactory, PortRegistry,
            FrSonicMidi.OnLayerSelect,
            FrSonicMidi.OnChannelSelect,
            FrSonicMidi.OnDialSectionModeSelect,
            FrSonicMidi.OnFilterParamsSectionSelect,
            FrSonicMidi.OnFilterParamsSectionMoveRight,
            FrSonicMidi.OnFilterParamsSectionMoveLeft);

        _processingThread = new(ProcessPipewireUpdatesThread);
        _processingThread.Start();
    }

    public static void Stop()
    {
        FrSonicLib.StopC();
        _running = false;
        _cts.Cancel();
    }

    /* ── event processing ────────────────────────────────────────────────── */

    private static void ProcessPipewireUpdatesThread()
    {
        try
        {
            while (_running)
            {
                var message = UpdatesFromPipewire.Take(_cts.Token);

                switch (message)
                {
                    case MetadataUpdateMessageBase metadataUpdate:
                        ProcessMetadataUpdate(metadataUpdate);
                        break;
                    case ParamsUpdatedMessage paramsUpdatedMessage:
                        ProcessParamsUpdate(paramsUpdatedMessage);
                        break;
                    case PropInfosUpdatedMessage propertyInfosUpdatedMessage:
                        ProcessPropertyInfoAddedMessage(
                            propertyInfosUpdatedMessage.propInfos);
                        break;
                    case PropsChangesMessage propsChangesMessage:
                        ProcessPropsUpdatedMessage(propsChangesMessage.props);
                        break;
                    case IWireplumberObjectAddedMessages objectAddedMessage:
                        ProcessObjectAddedMessage(objectAddedMessage);
                        break;
                    case ObjectDeletedMessage deletedMessage:
                        ProcessObjectDeletedMessage(deletedMessage);
                        break;
                    case PropsEnumFailedMessage propsEnumFailedMessage:
                        ProcessPropsEnumFailedMessage(propsEnumFailedMessage);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
    }

    private static void ProcessPropsEnumFailedMessage(PropsEnumFailedMessage message)
    {
        NodeRegistry.FailParams(message.ObjectSerial);
        NodeRegistry.FailProperties(message.ObjectSerial);
    }

    private static void ProcessPropsUpdatedMessage(Properties properties)
    {
        var objectSerial = properties.ObjectSerial;
        _props.AddOrUpdate(objectSerial, properties, (_, _) => properties);
        NodeRegistry.UpdateProperties(properties);
    }

    private static void ProcessPropertyInfoAddedMessage(
        PropertyInfoCollection propertyInfos)
    {
        var objectSerial = propertyInfos.ObjectSerial;
        _propInfos.TryAdd(objectSerial, propertyInfos);
        NodeRegistry.UpdatePropertyInfos(propertyInfos);
    }

    private static void ProcessParamsUpdate(ParamsUpdatedMessage paramUpdate)
    {
        var objectSerial = paramUpdate.ObjectSerial;
        var mergedParams = _params.AddOrUpdate(objectSerial, paramUpdate,
            (_, old) =>
            {
                var merged = new Dictionary<string, IParameter>(old.Parameters);
                foreach (var kvp in paramUpdate.Parameters)
                    merged[kvp.Key] = kvp.Value;
                return new(objectSerial, merged);
            });
        NodeRegistry.UpdateParams(mergedParams);
    }

    private static void ProcessMetadataUpdate(MetadataUpdateMessageBase metadataUpdate)
    {
        switch (metadataUpdate)
        {
            case MetadataAddedMessage m:
                MetadataRegistry.Add(m.MetadataName);
                break;
            case MetadataEntryUpdatedMessage m:
                MetadataRegistry.AddOrUpdateMetadataEntry(new(
                    m.MetadataName, m.Subject, m.Key, m.Type, m.Value));
                break;
            case MetadataEntryDeletedMessage m:
                MetadataRegistry.DeleteMetadataEntry(m.MetadataName, m.Subject, m.Key);
                break;
        }
    }

    private static void ProcessObjectAddedMessage(IWireplumberObjectAddedMessages data)
    {
        switch (data)
        {
            case WireplumberDeviceAddedMessage deviceMessage:
                DeviceRegistry.Add(deviceMessage.Device,
                    deviceMessage.TaskCompletionSources);

                var nodes = NodeRegistry.FindByDeviceId(deviceMessage.Device.ObjectId);
                if (nodes.Count != 0)
                {
                    DeviceRegistry.UpdateNodesList(
                        deviceMessage.Device.ObjectSerial, nodes);
                    NodeRegistry.UpdateDevices(
                        nodes.Select(n => n.ObjectSerial),
                        deviceMessage.Device);
                }
                break;

            case WireplumberNodeAddedMessage nodeMessage:
                var collection = MetadataRegistry.GetByName("default");
                var applicableMetadata = collection?.MetadataEntries
                    .Where(pair => pair.Subject == nodeMessage.Node.ObjectId);
                foreach (var entry in applicableMetadata ?? [])
                    nodeMessage.Node.Metadata.AddOrUpdate(entry);

                NodeRegistry.Add(nodeMessage.Node, nodeMessage.TaskCompletionSources);
                _moduleFactory.UpdateNodesForWaitingModules(nodeMessage.Node);
                _looperFactory.UpdateNodesForWaitingLoopers(nodeMessage.Node);

                var serial = nodeMessage.Node.ObjectSerial;
                if (_propInfos.TryGetValue(serial, out var propInfos))
                    NodeRegistry.UpdatePropertyInfos(propInfos);
                if (_params.TryGetValue(serial, out var parameters))
                    NodeRegistry.UpdateParams(parameters);
                if (_props.TryGetValue(serial, out var properties))
                    NodeRegistry.UpdateProperties(properties);

                if (nodeMessage.Node.DeviceAssignment is not null)
                {
                    DeviceRegistry.AddToNodeListByObjectId(
                        nodeMessage.Node.DeviceAssignment.Id, nodeMessage.Node);
                    var device = DeviceRegistry.GetByObjectId(
                        nodeMessage.Node.DeviceAssignment.Id);
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

    private static void ProcessObjectDeletedMessage(ObjectDeletedMessage deletedObject)
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
