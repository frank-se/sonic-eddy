using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Registries;

namespace Fr.Wireplumber.Tests.Registries;

internal record StoredObject(ulong ObjectId, ulong ObjectSerial)
    : IWireplumberObject
{
    public void TriggerDeletedEvent() => Deleted?.Invoke();

    public event Action? Deleted;
}

internal record CompletionSources();

internal enum ChangeType
{
    None,
    Property
};

public class GenericRegistryTests
{
    private bool _eventTriggered;
    private ulong _objectSerial = 0;

    private void HandleEvent(StoredObject storedObject)
    {
        _eventTriggered = true;
        _objectSerial = storedObject.ObjectSerial;
    }

    [Fact]
    public void AddTest()
    {
        var registry =
            new Registry<StoredObject, CompletionSources, ChangeType>();

        registry.Added += HandleEvent;

        registry.Add(new(12, 45), new());

        Assert.True(_eventTriggered);
        Assert.Equal(45ul, _objectSerial);

        registry.Added -= HandleEvent;
    }

    [Fact]
    public void DeleteTest()
    {
        const ulong objectSerial = 33;

        var registry =
            new Registry<StoredObject, CompletionSources, ChangeType>();

        registry.Deleted += HandleEvent;

        registry.Add(new(12, objectSerial), new());
        registry.Delete(objectSerial);

        Assert.True(_eventTriggered);
        Assert.Equal(objectSerial, _objectSerial);

        registry.Deleted -= HandleEvent;
    }

    private ChangeType _changeType = ChangeType.None;

    private void HandleUpdateEvent(StoredObject storedObject,
        ChangeType changeType)
    {
        HandleEvent(storedObject);
        _changeType = changeType;
    }

    private bool _triggerChangeEventCalled;
    private ulong _triggerChangeEventObjectSerial = 0;

    [Fact]
    public void TcsCompletionDoesNotTriggerChangeEventTest()
    {
        const ulong objectSerial = 325;

        var registry =
            new Registry<StoredObject, CompletionSources, ChangeType>();

        registry.Add(new(12, objectSerial), new());

        registry.Updated += HandleUpdateEvent;

        registry.UpdateByObjectSerial(objectSerial, _ => _, _ => true, @object =>
        {
            _triggerChangeEventCalled = true;
            _triggerChangeEventObjectSerial = @object.ObjectSerial;
        }, ChangeType.Property);
        
        Assert.False(_eventTriggered);
        Assert.Equal(0ul, _objectSerial);
        
        Assert.False(_triggerChangeEventCalled);
        Assert.Equal(0ul, _triggerChangeEventObjectSerial);
    }

    [Fact]
    public void BuildNewDoesTriggerChangeEventTest()
    {
        const ulong objectSerial = 6453;

        var registry =
            new Registry<StoredObject, CompletionSources, ChangeType>();

        registry.Add(new(12, objectSerial), new());

        registry.Updated += HandleUpdateEvent;

        registry.UpdateByObjectSerial(objectSerial, _ => _, _ => false, @object =>
        {
            _triggerChangeEventCalled = true;
            _triggerChangeEventObjectSerial = @object.ObjectSerial;
        }, ChangeType.Property);
        
        Assert.True(_eventTriggered);
        Assert.Equal(objectSerial, _objectSerial);
        
        Assert.True(_triggerChangeEventCalled);
        Assert.Equal(objectSerial, _triggerChangeEventObjectSerial);
    }
}