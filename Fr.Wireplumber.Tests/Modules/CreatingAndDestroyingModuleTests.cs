using Fr.Wireplumber.Model.Config.LoopbackModule;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules;
using Fr.Wireplumber.Tests.Shared;

namespace Fr.Wireplumber.Tests.Modules;

[Collection("WireplumberCollection")]
public class CreatingAndDestroyingModuleTests : IDisposable
{
    private readonly List<ulong> _seenObjectSerials = [];
    private readonly CountdownEvent _countdown = new(2);

    public CreatingAndDestroyingModuleTests()
    {
        Fr.Wireplumber.Wireplumber.NodeRegistry.Added += TestNodeAddedListener;
        Fr.Wireplumber.Wireplumber.NodeRegistry.Deleted +=
            TestNodeDeletedListener;
    }

    private void TestNodeAddedListener(Node node)
    {
        _seenObjectSerials.Add(node.ObjectSerial);
    }

    private void TestNodeDeletedListener(Node node)
    {
        if (_seenObjectSerials.Contains(node.ObjectSerial))
        {
            _countdown.Signal();
        }
    }

    [Fact]
    public async Task CreateLoopbackModule()
    {
        const string loopbackModuleName = "test-loopback-module";

        var loopbackModuleConfig = new LoopbackModuleConfig
        {
            CaptureProps = new()
            {
                Linger = true,
                AutoConnect = false,
                Name = $"{loopbackModuleName}-capture",
                Description = $"{loopbackModuleName}-capture",
                MediaClass = "Stream/Input/Audio",
            },
            PlaybackProps = new()
            {
                Name = $"{loopbackModuleName}-playback",
                Description = $"{loopbackModuleName}-playback",
                MediaClass = "Stream/Output/Audio",
                Passive = true
            }
        };

        var loopbackModule =
            await Fr.Wireplumber.Wireplumber.ModuleFactory
                .CreateLoopbackModuleAsync("test", loopbackModuleConfig);

        var nodeRegistry = Fr.Wireplumber.Wireplumber.NodeRegistry;
        Assert.NotNull(
            nodeRegistry.GetByObjectSerial(loopbackModule
                .CaptureNodeObjectSerial));

        Assert.NotNull(
            nodeRegistry.GetByObjectSerial(loopbackModule
                .PlaybackNodeObjectSerial));

        Assert.Contains(_seenObjectSerials,
            s => s == loopbackModule.CaptureNodeObjectSerial);

        Assert.Contains(_seenObjectSerials,
            s => s == loopbackModule.PlaybackNodeObjectSerial);

        loopbackModule.Destroy();

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var countdownTask = Task.Run(() => _countdown.Wait());
        var completed = await Task.WhenAny(timeoutTask, countdownTask);
        if (completed == timeoutTask)
            Assert.Fail("Timeout triggered, events didn't fire");

        Assert.Equal(0, _countdown.CurrentCount);
    }

    [Fact]
    public async Task CreateAndDestroyFilterGraph()
    {
        var filterGraphConfig = SharedTestingResources.FilterChainConfig;

        var filterGraph =
            await Fr.Wireplumber.Wireplumber.ModuleFactory
                .CreateFilterChainAsync("test", filterGraphConfig);

        var nodeRegistry = Fr.Wireplumber.Wireplumber.NodeRegistry;
        Assert.NotNull(
            nodeRegistry.GetByObjectSerial(filterGraph
                .CaptureNodeObjectSerial));

        Assert.NotNull(
            nodeRegistry.GetByObjectSerial(filterGraph
                .PlaybackNodeObjectSerial));

        Assert.Contains(_seenObjectSerials,
            s => s == filterGraph.CaptureNode.ObjectSerial);

        Assert.Contains(_seenObjectSerials,
            s => s == filterGraph.PlaybackNode.ObjectSerial);

        filterGraph.Destroy();

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var countdownTask = Task.Run(() => _countdown.Wait());
        var completed = await Task.WhenAny(timeoutTask, countdownTask);
        if (completed == timeoutTask)
            Assert.Fail("Timeout triggered, events didn't fire");

        Assert.Equal(0, _countdown.CurrentCount);
    }

    public void Dispose()
    {
        Fr.Wireplumber.Wireplumber.NodeRegistry.Added -= TestNodeAddedListener;
        Fr.Wireplumber.Wireplumber.NodeRegistry.Deleted -=
            TestNodeDeletedListener;
    }
}