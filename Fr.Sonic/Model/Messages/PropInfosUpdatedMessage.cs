using Fr.Sonic.Model.PropInfo;

namespace Fr.Sonic.Model.Messages;

internal record PropInfosUpdatedMessage(PropertyInfoCollection propInfos)
    : IMessage;