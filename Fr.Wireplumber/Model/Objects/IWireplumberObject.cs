namespace Fr.Wireplumber.Model.Objects;

/// <summary>
/// Information shared by all pipewire Objects
/// </summary>
public interface IWireplumberObject
{
    /// <summary>
    /// Object id. The id is assigned by pipewire. Ids are reused, in order to
    /// present low ids for usage by the end user. Generally that is not a
    /// problem, but in code prefer the object serial instead, which is
    /// guaranteed to be unique
    /// </summary>
    ulong ObjectId { get; }
    
    /// <summary>
    /// Object serial, pipewire creates this id by incrementing a counter for
    /// every object, and guarantees it to be unique.
    /// </summary>
    ulong ObjectSerial { get; }

    /// <summary>
    /// Trigger deletion event
    /// </summary>
    void TriggerDeletedEvent();

    /// <summary>
    /// Deleted event. Triggered when the object is deleted.
    /// </summary>
    event Action? Deleted;
}