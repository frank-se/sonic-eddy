using Fr.Wireplumber.Model.Config.FilterChain;

namespace Fr.Wireplumber.Tests.Shared;

public static class SharedTestingResources
{
    public static FilterChainModuleConfig FilterChainConfig =>
        new FilterChainModuleConfig
        {
            MediaName = "pmx",
            LinkGroup = "pmx_filter",
            CaptureProps = new()
            {
                Linger = true,
                AutoConnect = true,
                DontFallback = true,
                Name = "test-filter-graph-capture",
                Description = "test-filter-graph-capture",
                MediaClass = "Stream/Input/Audio",
            },
            PlaybackProps = new()
            {
                AutoConnect = true,
                Name = "test-filter-graph-playback",
                Description = "test-filter-graph-playback",
                MediaClass = "Stream/Output/Audio",
            },
            FilterGraph = new()
            {
                Nodes =
                [
                    new()
                    {
                        Name = "Compressor",
                        Type = "lv2",
                        Plugin =
                            "http://calf.sourceforge.net/plugins/Compressor",
                        Control = new()
                        {
                            { "bypass", 0 },
                            { "level_in", 1 },
                            { "threshold", 0.1 },
                            { "ratio", 12 },
                            { "attack", 12 },
                            { "release", 270.0 },
                            { "makeup", 1.2 },
                            { "knee", 2.3 },
                            { "detection", 0.0 },
                            { "stereo_link", 0.0 },
                            { "mix", 0.75 },
                        }
                    }
                ],
                Links = [],
                Inputs = ["Compressor:in_l", "Compressor:in_r"],
                Outputs = ["Compressor:out_l", "Compressor:out_r"],
            }
        };
}