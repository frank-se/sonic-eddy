namespace SonicEddy.Tests.Initialization;

public class Lv2InitFixture : IDisposable
{
    public Lv2InitFixture()
    {
        Fr.Lv2.Lv2.Init();
    }

    public void Dispose()
    {
        Fr.Lv2.Lv2.Destroy();
    }
}