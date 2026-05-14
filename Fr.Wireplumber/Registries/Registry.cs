using System.Collections.Concurrent;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.PInvoke;

// ReSharper disable MemberCanBeProtected.Global

namespace Fr.Wireplumber.Registries;

/// <summary>
/// Provides access to pipewire objects
/// </summary>
/// <typeparam name="TObject">
/// Type of the object.
/// </typeparam>
/// <typeparam name="TCompletionSources">
/// Completion sources for 
/// </typeparam>
/// <typeparam name="TChangeType"></typeparam>
public class Registry<TObject, TCompletionSources, TChangeType>
    where TObject : IWireplumberObject
{
    private readonly ConcurrentDictionary<ulong, TObject>
        _objectsByObjectSerial = [];

    private readonly ConcurrentDictionary<ulong, TObject>
        _objectsByObjectId = [];

    /// <summary>
    /// Gives read-only access to the stored objects
    /// </summary>
    public IReadOnlyCollection<TObject> Objects =>
        (IReadOnlyCollection<TObject>)_objectsByObjectSerial.Values;

    private readonly ConcurrentDictionary<ulong, TCompletionSources>
        _taskCompletionSources = [];

    /// <summary>
    /// Event is triggered when an object is added
    /// </summary>
    public event Action<TObject>? Added;

    /// <summary>
    /// Event is triggered when an object is deleted
    /// </summary>
    public event Action<TObject>? Deleted;

    /// <summary>
    /// Event is triggered when an object is updated
    /// </summary>
    public event Action<TObject, TChangeType>? Updated;

    /// <summary>
    /// Searches the object by object serial. Returns the object, or <c>null</c>
    /// if it doesn't exist.
    /// </summary>
    /// <param name="objectSerial">Object serial of the object</param>
    /// <returns>The store pipewire object, or <c>null</c></returns>
    public TObject? GetByObjectSerial(ulong objectSerial) =>
        _objectsByObjectSerial.GetValueOrDefault(objectSerial);

    /// <summary>
    /// Searches the object by object id. Returns the object, or <c>null</c>
    /// if it doesn't exist.
    /// </summary>
    /// <param name="objectId">Object id of the object</param>
    /// <returns>The store pipewire object, or <c>null</c></returns>
    public TObject? GetByObjectId(ulong objectId) =>
        _objectsByObjectId.GetValueOrDefault(objectId);

    internal bool Add(TObject @object, TCompletionSources tcs)
    {
        if (!_objectsByObjectSerial.TryAdd(@object.ObjectSerial, @object) ||
            !_taskCompletionSources.TryAdd(@object.ObjectSerial, tcs) ||
            !_objectsByObjectId.TryAdd(@object.ObjectId, @object))
            return false;

        Added?.Invoke(@object);
        return true;
    }

    internal void Delete(ulong objectSerial)
    {
        if (!_objectsByObjectSerial.TryRemove(objectSerial, out var @object))
            return;
        _objectsByObjectId.TryRemove(@object.ObjectId, out _);

        @object.TriggerDeletedEvent();
        Deleted?.Invoke(@object);
    }

    internal void UpdateByObjectSerial(ulong objectSerial,
        Func<TObject, TObject> buildNew,
        Func<TCompletionSources, bool> finishTask,
        Action<TObject> triggerChangeEvent,
        TChangeType changeType)
    {
        if (!_objectsByObjectSerial.TryGetValue(objectSerial, out var @object))
            return;
        if (!_taskCompletionSources.TryGetValue(objectSerial, out var tcs))
            return;

        if (finishTask(tcs))
            return;

        var newObject = buildNew(@object);
        _objectsByObjectSerial[objectSerial] = newObject;
        Updated?.Invoke(newObject, changeType);
        triggerChangeEvent(newObject);
    }

    internal void UpdateByObjectId(ulong objectId,
        Func<TObject, TObject> buildNew,
        Func<TCompletionSources, bool> finishTask,
        Action<TObject> triggerChangeEvent,
        TChangeType changeType)
    {
        if (!_objectsByObjectId.TryGetValue(objectId, out var @object))
            return;
        if (!_taskCompletionSources.TryGetValue(@object.ObjectSerial,
                out var tcs))
            return;

        if (finishTask(tcs))
            return;

        var newObject = buildNew(@object);
        _objectsByObjectId[objectId] = newObject;
        Updated?.Invoke(newObject, changeType);
        triggerChangeEvent(newObject);
    }

    internal void UpdateOnce(ulong objectSerial,
        Action<TCompletionSources> finishTask)
    {
        if (!_objectsByObjectSerial.TryGetValue(objectSerial, out var @object))
            return;
        if (!_taskCompletionSources.TryGetValue(objectSerial, out var tcs))
            return;

        finishTask(tcs);
    }
}