using Fr.Wireplumber.Model.PropInfo;

namespace Fr.Wireplumber.Model.Messages;

internal record PropInfosUpdatedMessage(PropertyInfoCollection propInfos)
    : IMessage;