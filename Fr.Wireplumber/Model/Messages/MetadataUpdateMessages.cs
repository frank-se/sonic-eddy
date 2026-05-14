namespace Fr.Wireplumber.Model.Messages;

internal abstract record MetadataUpdateMessageBase(string MetadataName)
    : IMessage;

internal record MetadataAddedMessage(string MetadataName)
    : MetadataUpdateMessageBase(MetadataName);

internal record MetadataEntryUpdatedMessage(
    string MetadataName,
    ulong Subject,
    string Key,
    string Type,
    string Value) : MetadataUpdateMessageBase(MetadataName);

internal record MetadataEntryDeletedMessage(
    string MetadataName,
    ulong Subject,
    string Key) : MetadataUpdateMessageBase(MetadataName);