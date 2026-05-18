using System.Collections.Concurrent;
using Fr.Sonic.PInvoke;

namespace Fr.Sonic.Model.Metadata;

/// <summary>
/// A collection of metadata entries
/// </summary>
/// <param name="Name">The name of the metadata collection</param>
public record MetadataCollection(string Name)
{
    private readonly ConcurrentDictionary<(ulong Subject, string Key),
            MetadataEntry>
        _metadataEntries = [];

    /// <summary>
    /// All metadata entries in the collection
    /// </summary>
    public IReadOnlyCollection<MetadataEntry> MetadataEntries =>
        (IReadOnlyCollection<MetadataEntry>)_metadataEntries.Values;

    /// <summary>
    /// Event is triggered when new metadata is added to the collection
    /// </summary>
    public event Action<MetadataEntry>? Added;

    /// <summary>
    /// Event is triggered when a new metadata entry is added to the collection.
    /// </summary>
    public event Action<MetadataEntry>? Updated;

    /// <summary>
    /// Event is triggered when a new metadata entry is removed rom the collection.
    /// </summary>
    public event Action<MetadataEntry>? Deleted;

    /// <summary>
    /// Set a metadata entry in pipewire. This is asynchronous, the metadata
    /// collection will be updated once the update is processed by pipewire.
    /// </summary>
    /// <param name="subject"><see cref="MetadataEntry.Subject" /></param>
    /// <param name="key"><see cref="MetadataEntry.Key" /></param>
    /// <param name="type"><see cref="MetadataEntry.Type" /></param>
    /// <param name="value"><see cref="MetadataEntry.Value" /></param>
    public void AddOrUpdateMetadataEntry(ulong subject, string key, string type,
        string value) =>
        FrSonicLib.SetMetadataEntryC(Name, subject, key, type, value);

    /// <summary>
    /// Delete a metadata entry
    /// </summary>
    /// <param name="subject"><see cref="MetadataEntry.Subject" /></param>
    /// <param name="key"><see cref="MetadataEntry.Key" /></param>
    public void DeleteMetadataEntry(ulong subject, string key) =>
        FrSonicLib.DeleteMetadataEntryC(Name, subject, key);

    internal void AddOrUpdate(MetadataEntry metadataEntry)
    {
        var updated = false;
        _metadataEntries.AddOrUpdate(
            (Subject: metadataEntry.Subject, Key: metadataEntry.Key),
            _ => metadataEntry, (_, _) =>
            {
                updated = true;
                return metadataEntry;
            });

        if (updated)
        {
            Updated?.Invoke(metadataEntry);
        }
        else
        {
            Added?.Invoke(metadataEntry);
        }
    }

    internal void Delete(ulong subject, string key)
    {
        if (_metadataEntries.TryRemove((subject, key), out var metadata))
        {
            Deleted?.Invoke(metadata);
        }
    }
}