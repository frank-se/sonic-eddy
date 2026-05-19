using Fr.Sonic;

namespace SonicEddy.Tests.Initialization;

public class Lv2InitFixture : IDisposable
{
    public Lv2InitFixture()
    {
        FrSonicLv2.Init();
    }

    public void Dispose()
    {
        FrSonicLv2.Destroy();
    }
}