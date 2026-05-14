using Fr.Wireplumber.Model.Params;

namespace Fr.Wireplumber.Model.Messages;

internal record ParamsUpdatedMessage(
    ulong ObjectSerial,
    Dictionary<string, IParameter> Parameters) : IMessage;