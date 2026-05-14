using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params;
using Fr.Wireplumber.Model.PropInfo;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Registries.Devices;
using Fr.Wireplumber.Registries.Nodes;

namespace Fr.Wireplumber.Model.Messages;

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