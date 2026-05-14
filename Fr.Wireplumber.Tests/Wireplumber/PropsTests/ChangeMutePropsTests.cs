using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules;
using Fr.Wireplumber.Tests.Shared;

namespace Fr.Wireplumber.Tests.Wireplumber.PropsTests;

[Collection("WireplumberCollection")]
public class ChangeMutePropsTests
{
    private readonly CountdownEvent _initialCountdown = new(2);
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
    public async Task ChangeFilterGraphMute()
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

        filterGraph.PlaybackNode.SetMute(true);

        completed = await Task.WhenAny(timeoutTask, countdownTask);
        if (completed == timeoutTask)
            Assert.Fail("Timeout triggered, events didn't fire");

        var properties = await filterGraph.PlaybackNode.Properties;

        Assert.True(properties?.Mute);
    }
}