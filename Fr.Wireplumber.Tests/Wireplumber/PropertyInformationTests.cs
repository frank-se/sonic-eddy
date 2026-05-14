using Fr.Wireplumber.Helper;
using Fr.Wireplumber.Modules;
using Fr.Wireplumber.Tests.Shared;

namespace Fr.Wireplumber.Tests.Wireplumber;

[Collection("WireplumberCollection")]
public class PropertyInformationTests
{
    [Fact]
    public async Task FilterGraphPropertyInfo()
    {
        var filterGraphConfig = SharedTestingResources.FilterChainConfig;

        var filterGraph =
            await Fr.Wireplumber.Wireplumber.ModuleFactory
                .CreateFilterChainAsync("test", filterGraphConfig);

        var propInfos = await filterGraph.CaptureNode.PropertyInfos;

        Assert.Equal(propInfos.ObjectSerial,
            filterGraph.CaptureNode.ObjectSerial);
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "volume");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "mute");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelVolumes");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "channelMap");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "monitorMute");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "monitorVolumes");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "softMute");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "softVolumes");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "monitor.channel-volumes");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.disable");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.min-volume");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.max-volume");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.normalize");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.mix-lfe");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.upmix");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.lfe-cutoff");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.fc-cutoff");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.rear-delay");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.stereo-widen");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.hilbert-taps");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.upmix-method");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "rate");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "quality");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "resample.disable");
        Assert.Contains(propInfos.PropertyInfos, p => p.Name == "dither.noise");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "dither.method");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "debug.wav-path");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "channelmix.lock-volumes");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "audioconvert.filter-graph.disable");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "audioconvert.filter-graph");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:bypass");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:level_in");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:threshold");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:ratio");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:attack");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:release");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:makeup");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:knee");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:detection");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:stereo_link");
        Assert.Contains(propInfos.PropertyInfos,
            p => p.Name == "Compressor:mix");
    }
}