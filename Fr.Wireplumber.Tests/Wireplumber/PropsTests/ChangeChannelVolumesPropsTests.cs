using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules;
using Fr.Wireplumber.Tests.Shared;

namespace Fr.Wireplumber.Tests.Wireplumber.PropsTests;

[Collection("WireplumberCollection")]
public class ChangeChannelVolumesPropsTests
{
    private readonly CountdownEvent _initialCountdown = new(3);
    private readonly CountdownEvent _countdown = new(1);

    private void InitialCountdownListener(Properties? properties)
    {
        try
        {
            _initialCountdown.Signal();
        } 
        catch (InvalidOperationException)
        {
            // ignore more signals
        }
    }

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
    }

    [Fact]
    public async Task ChangeFilterGraphVolume()
    {
        var filterGraphConfig = SharedTestingResources.FilterChainConfig;

        var filterGraph =
            await Fr.Wireplumber.Wireplumber.ModuleFactory
                .CreateFilterChainAsync("test", filterGraphConfig);

        filterGraph.PlaybackNode.PropertiesChanged +=
            InitialCountdownListener;

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var countdownTask = Task.Run(() => _initialCountdown.Wait());
        var completed = await Task.WhenAny(timeoutTask, countdownTask);
        if (completed == timeoutTask)
            Assert.Fail("Timeout triggered, events didn't fire");

        filterGraph.PlaybackNode.PropertiesChanged -=
            InitialCountdownListener;

        filterGraph.PlaybackNode.PropertiesChanged += PropsChangedListener;
        timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        countdownTask = Task.Run(() => _countdown.Wait());

        filterGraph.PlaybackNode.SetVolumes([0.44, 0.1]);

        completed = await Task.WhenAny(timeoutTask, countdownTask);
        if (completed == timeoutTask)
            Assert.Fail("Timeout triggered, events didn't fire");

        filterGraph.PlaybackNode.PropertiesChanged -= PropsChangedListener;

        var properties = await filterGraph.PlaybackNode.Properties;

        Assert.Equal(2, properties?.Channels.Count);
        Assert.Equal(0.44, properties!.Channels[0].Volume, tolerance: 0.00001);
        Assert.Equal(0.1, properties!.Channels[1].Volume, tolerance: 0.00001);
    }
}