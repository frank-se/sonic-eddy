using Fr.Wireplumber.Model.Props;

namespace Fr.Wireplumber.Model.Messages;

internal record PropsChangesMessage(Properties props) : IMessage;