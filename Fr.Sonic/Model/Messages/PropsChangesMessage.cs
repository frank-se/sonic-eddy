using Fr.Sonic.Model.Props;

namespace Fr.Sonic.Model.Messages;

internal record PropsChangesMessage(Properties props) : IMessage;