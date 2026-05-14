using Fr.Wireplumber.PInvoke;

namespace Fr.Wireplumber.Model.Messages;

internal record ObjectDeletedMessage(
    ulong ObjectSerial,
    wireplumber_object_type ObjectType) : IMessage;