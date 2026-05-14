namespace Fr.Wireplumber.Tests.Initialization;

public class WireplumberInitFixture : IDisposable
{
    public WireplumberInitFixture()
    {
        Fr.Wireplumber.Wireplumber.Start();
    }

    public void Dispose()
    {
        Fr.Wireplumber.Wireplumber.Stop();
    }
}