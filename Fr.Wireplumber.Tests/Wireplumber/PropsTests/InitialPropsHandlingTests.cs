using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules;
using Fr.Wireplumber.Tests.Shared;

namespace Fr.Wireplumber.Tests.Wireplumber.PropsTests;

[Collection("WireplumberCollection")]
public class InitialPropsHandlingTests
{
    private readonly CountdownEvent _countdown = new(2);
    private readonly List<Properties?> _seenProperties = [];

    private void PropsChangedListener(Properties? properties)
    {
        try
        {
            _countdown.Signal();
        }
        catch (InvalidOperationException)
        {
            // ignore more signals
        }

        _seenProperties.Add(properties);
    }

    [Fact]
    public async Task FilterGraphProps()
    {
        var filterGraphConfig = SharedTestingResources.FilterChainConfig;

        var filterGraph =
            await Fr.Wireplumber.Wireplumber.ModuleFactory
                .CreateFilterChainAsync("test", filterGraphConfig);

        filterGraph.PlaybackNode.PropertiesChanged +=
            PropsChangedListener;

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var countdownTask = Task.Run(() => _countdown.Wait());
        var completed = await Task.WhenAny(timeoutTask, countdownTask);
        if (completed == timeoutTask)
            Assert.Fail("Timeout triggered, events didn't fire");

        filterGraph.PlaybackNode.PropertiesChanged -=
            PropsChangedListener;

        Assert.Equal(0, _countdown.CurrentCount);

        var lastProperties = _seenProperties.Last();
        Assert.Equal(2, lastProperties?.Channels.Count);
        Assert.Equal(1, lastProperties?.Channels[0].Volume);
        Assert.Equal(1, lastProperties?.Channels[1].Volume);

        var nodeProperties = await filterGraph.PlaybackNode.Properties;
        Assert.Equal(2, nodeProperties!.Channels.Count);

        filterGraph.Destroy();

        filterGraph.PlaybackNode.PropertiesChanged -=
            PropsChangedListener;
    }
}