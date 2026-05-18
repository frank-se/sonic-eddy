using Fr.Sonic.Model.Params;

namespace Fr.Sonic.Model.Messages;

internal record ParamsUpdatedMessage(
    ulong ObjectSerial,
    Dictionary<string, IParameter> Parameters) : IMessage;