using Fr.Wireplumber.Model.Params;
using Fr.Wireplumber.Modules;
using Fr.Wireplumber.Tests.Shared;

namespace Fr.Wireplumber.Tests.Wireplumber;

[Collection("WireplumberCollection")]
public class ParamsTests
{
    [Fact]
    public async Task FilterGraphParams()
    {
        var filterGraphConfig = SharedTestingResources.FilterChainConfig;

        var filterGraph =
            await Fr.Wireplumber.Wireplumber.ModuleFactory
                .CreateFilterChainAsync("test", filterGraphConfig);

        var parameters = await filterGraph.CaptureNode.Params;

        Assert.True(
            parameters!.TryGetValue("Compressor:bypass", out var bypass));
        Assert.Equal(0.0f, (bypass as Parameter<float>)?.Value);

        Assert.True(
            parameters.TryGetValue("Compressor:level_in", out var levelIn));
        Assert.Equal(1.0f, (levelIn as Parameter<float>)?.Value);

        Assert.True(parameters.TryGetValue("Compressor:threshold",
            out var threshold));
        Assert.Equal(0.1f, (threshold as Parameter<float>)?.Value);

        Assert.True(parameters.TryGetValue("Compressor:ratio", out var ratio));
        Assert.Equal(12.0f, (ratio as Parameter<float>)?.Value);

        Assert.True(
            parameters.TryGetValue("Compressor:attack", out var attack));
        Assert.Equal(12.0f, (attack as Parameter<float>)?.Value);

        Assert.True(
            parameters.TryGetValue("Compressor:release", out var release));
        Assert.Equal(270.0f, (release as Parameter<float>)?.Value);

        Assert.True(
            parameters.TryGetValue("Compressor:makeup", out var makeup));
        Assert.Equal(1.2f, (makeup as Parameter<float>)?.Value);

        Assert.True(parameters.TryGetValue("Compressor:knee", out var knee));
        Assert.Equal(2.3f, (knee as Parameter<float>)?.Value);

        Assert.True(parameters.TryGetValue("Compressor:detection",
            out var detection));
        Assert.Equal(0.0f, (detection as Parameter<float>)?.Value);

        Assert.True(parameters.TryGetValue("Compressor:stereo_link",
            out var stereoLink));
        Assert.Equal(0.0f, (stereoLink as Parameter<float>)?.Value);

        Assert.True(parameters.TryGetValue("Compressor:mix", out var mix));
        Assert.Equal(0.75f, (mix as Parameter<float>)?.Value);

        filterGraph.Destroy();
    }
}