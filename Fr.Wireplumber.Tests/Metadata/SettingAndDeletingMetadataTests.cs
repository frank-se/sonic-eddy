using Fr.Wireplumber.Model.Metadata;

namespace Fr.Wireplumber.Tests.Metadata;

[Collection("WireplumberCollection")]
public class SettingAndDeletingMetadataTests : IDisposable
{
    private readonly CountdownEvent _metadataAddedCountdownEvent = new(1);

    public SettingAndDeletingMetadataTests()
    {
        Fr.Wireplumber.Wireplumber.MetadataRegistry.Added +=
            TestMetadataAddedListener;
    }

    private void TestMetadataAddedListener(MetadataCollection collection)
    {
        if (collection.Name != "default") return;
        _metadataAddedCountdownEvent.Signal();
    }

    private readonly List<MetadataEntry> _metadataAddedMessages = [];
    private readonly CountdownEvent _addedCountdownEvent = new(1);

    private void TestMetadataEntryAddedListener(MetadataEntry metadata)
    {
        _metadataAddedMessages.Add(metadata);
        _addedCountdownEvent.Signal();
    }

    private readonly List<MetadataEntry> _metadataUpdatedMessages = [];
    private readonly CountdownEvent _updatedCountdownEvent = new(1);

    private void TestMetadataEntryUpdatedListener(MetadataEntry metadata)
    {
        _metadataUpdatedMessages.Add(metadata);
        _updatedCountdownEvent.Signal();
    }

    private readonly List<MetadataEntry> _metadataDelectedMessages = [];
    private readonly CountdownEvent _deletedCountdownEvent = new(1);

    private void
        TestMetadataEntryDeletedListener(MetadataEntry metadata)
    {
        _metadataDelectedMessages.Add(metadata);
        _deletedCountdownEvent.Signal();
    }

    [Fact]
    public async Task SetMetadata()
    {
        const string metadataName = "default";
        var collection =
            Fr.Wireplumber.Wireplumber.MetadataRegistry.GetByName(metadataName);

        if (collection is null)
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var countdownTask =
                Task.Run(() => _metadataAddedCountdownEvent.Wait());
            var completed = await Task.WhenAny(timeoutTask, countdownTask);
            if (completed == timeoutTask)
                Assert.Fail("Timeout triggered, but collection was not added");
        }

        collection =
            Fr.Wireplumber.Wireplumber.MetadataRegistry.GetByName(metadataName);

        Assert.NotNull(collection);

        collection.Added += TestMetadataEntryAddedListener;
        collection.Updated += TestMetadataEntryUpdatedListener;
        collection.Deleted += TestMetadataEntryDeletedListener;

        collection.AddOrUpdateMetadataEntry(0, "my_test", "Spa:String:JSON",
            "test_value");

        var addedTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var addedCountdownTask =
            Task.Run(() => _addedCountdownEvent.Wait());
        var addedCompleted =
            await Task.WhenAny(addedTimeoutTask, addedCountdownTask);
        if (addedCompleted == addedTimeoutTask)
            Assert.Fail("Timeout triggered, but metadata was not updated");

        var addedEntry = _metadataAddedMessages.First();

        Assert.Equal(0ul, addedEntry.Subject);
        Assert.Equal("my_test", addedEntry.Key);
        Assert.Equal("Spa:String:JSON", addedEntry.Type);
        Assert.Equal("test_value", addedEntry.Value);

        collection.AddOrUpdateMetadataEntry(0, "my_test", "Spa:String:JSON",
            "test_value_2");

        var updatedTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var updatedCountdownTask =
            Task.Run(() => _updatedCountdownEvent.Wait());
        var updatedCompleted =
            await Task.WhenAny(updatedTimeoutTask, updatedCountdownTask);
        if (updatedCompleted == updatedTimeoutTask)
            Assert.Fail("Timeout triggered, but metadata was not updated");

        var updatedEntry = _metadataUpdatedMessages.First();

        Assert.Equal(0ul, updatedEntry.Subject);
        Assert.Equal("my_test", updatedEntry.Key);
        Assert.Equal("Spa:String:JSON", updatedEntry.Type);
        Assert.Equal("test_value_2", updatedEntry.Value);

        collection.DeleteMetadataEntry(0, "my_test");

        var deletedTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var deletedCountdownTask =
            Task.Run(() => _deletedCountdownEvent.Wait());
        var deletedCompleted =
            await Task.WhenAny(deletedTimeoutTask, deletedCountdownTask);
        if (deletedCompleted == deletedTimeoutTask)
            Assert.Fail("Timeout triggered, but metadata was not deleted");

        var deletedEntry = _metadataDelectedMessages.First();

        Assert.Equal(0ul, deletedEntry.Subject);
        Assert.Equal("my_test", deletedEntry.Key);
        Assert.Equal("Spa:String:JSON", deletedEntry.Type);
        Assert.Equal("test_value_2", deletedEntry.Value);

        collection.Added -= TestMetadataEntryAddedListener;
        collection.Updated -= TestMetadataEntryUpdatedListener;
        collection.Deleted -= TestMetadataEntryDeletedListener;
    }

    public void Dispose()
    {
        Fr.Wireplumber.Wireplumber.MetadataRegistry.Added -=
            TestMetadataAddedListener;
    }
}