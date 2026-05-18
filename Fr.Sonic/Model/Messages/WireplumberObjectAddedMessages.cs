using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using Fr.Sonic.Model.PropInfo;
using Fr.Sonic.Model.Props;
using Fr.Sonic.Registries.Devices;
using Fr.Sonic.Registries.Nodes;

namespace Fr.Sonic.Model.Messages;

internal interface IWireplumberObjectAddedMessages : IMessage;

internal record WireplumberDeviceAddedMessage(
    Device Device,
    DeviceTaskCompletionSources TaskCompletionSources)
    : IWireplumberObjectAddedMessages;

internal record WireplumberNodeAddedMessage(
    Node Node,
    NodeTaskCompletionSources TaskCompletionSources)
    : IWireplumberObjectAddedMessages;

internal record WireplumberPortAddedMessage(Port Port)
    : IWireplumberObjectAddedMessages;

internal record WireplumberLinkAddedMessage(Link Link)
    : IWireplumberObjectAddedMessages;

internal record WireplumberClientAddedMessage(Client Client)
    : IWireplumberObjectAddedMessages;