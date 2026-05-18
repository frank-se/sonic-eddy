using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Model.Messages;

internal record ObjectDeletedMessage(
    ulong ObjectSerial,
    wireplumber_object_type ObjectType) : IMessage;