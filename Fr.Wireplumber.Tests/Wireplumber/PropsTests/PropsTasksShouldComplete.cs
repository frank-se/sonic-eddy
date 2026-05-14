namespace Fr.Wireplumber.Tests.Wireplumber.PropsTests;


[Collection("WireplumberCollection")]
public class PropsTasksShouldComplete
{
    [Fact]
    public async Task InitialPropsShouldComplete()
    {
        var notCompleted =
            Fr.Wireplumber.Wireplumber.NodeRegistry.Objects.Where(node =>
                !node.Properties.IsCompleted);
        
        Assert.Empty(notCompleted);
    }
}