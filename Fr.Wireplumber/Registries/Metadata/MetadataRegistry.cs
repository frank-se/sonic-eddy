using System.Collections.Concurrent;
using Fr.Wireplumber.Model.Metadata;
using Fr.Wireplumber.Registries.Nodes;

namespace Fr.Wireplumber.Registries.Metadata;

/// <summary>
/// Provides access to pipewire metadata
/// </summary>
public class MetadataRegistry(NodeRegistry nodeRegistry)
{
    private readonly ConcurrentDictionary<string, MetadataCollection>
        _metadataCollections = [];

    /// <summary>
    /// All metadata collections in the registry
    /// </summary>
    public IReadOnlyCollection<MetadataCollection> MetadataCollections =>
        (IReadOnlyCollection<MetadataCollection>)_metadataCollections.Values;

    /// <summary>
    /// Event is triggered when a new metadata collection is added to the
    /// registry
    /// </summary>
    public event Action<MetadataCollection>? Added;

    /// <summary>
    /// Event is triggered when a metadata collection is changed
    /// </summary>
    public event Action<MetadataCollection>? Updated;

    /// <summary>
    /// Gets the metadata collection by name.
    /// </summary>
    /// <returns>
    /// The metadata collection, or <c>null</c> if it doesn't exist
    /// </returns>
    public MetadataCollection? GetByName(string key) =>
        _metadataCollections.GetValueOrDefault(key);

    internal MetadataCollection? Add(string metadataName)
    {
        if (_metadataCollections.ContainsKey(metadataName)) return null;

        var collection = new MetadataCollection(metadataName);
        if (!_metadataCollections.TryAdd(metadataName, collection))
            return null;

        Added?.Invoke(collection);
        return collection;
    }

    internal void AddOrUpdateMetadataEntry(MetadataEntry metadataEntry)
    {
        var collection = Add(metadataEntry.MetadataName) ??
                         _metadataCollections[metadataEntry.MetadataName];

        collection.AddOrUpdate(metadataEntry);
        Updated?.Invoke(collection);

        if (metadataEntry.MetadataName != "default" ||
            metadataEntry.Subject == 0) return;

        var node = nodeRegistry.GetByObjectId(metadataEntry.Subject);
        node?.Metadata.AddOrUpdate(metadataEntry);
    }

    internal void DeleteMetadataEntry(string metadataName, ulong subject,
        string key)
    {
        if (_metadataCollections.TryGetValue(metadataName, out var collection))
        {
            collection.Delete(subject, key);
        }

        if (metadataName != "default" || subject == 0) return;

        var node = nodeRegistry.GetByObjectId(subject);
        node?.Metadata.Delete(subject, key);
    }
}