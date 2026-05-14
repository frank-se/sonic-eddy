namespace Fr.Wireplumber.Model.Metadata;

/// <summary>
/// A pipewire metadata entry
/// </summary>
/// <param name="MetadataName">Name of the metadata</param>
/// <param name="Subject">
/// Subject of the metadata, this is either <c>0</c> for global metadata, e.g.
/// wireplumber configuration, like the default audio output, or the
/// <c>Object Id</c> of the object the metadata is associated with, e.g. to set
/// the target object of a node.
/// </param>
/// <param name="Key">Key of the metadata</param>
/// <param name="Type">Type of the metadata</param>
/// <param name="Value">Value of the metadata</param>
public record MetadataEntry(
    string MetadataName,
    ulong Subject,
    string Key,
    string Type,
    string Value);