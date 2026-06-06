using System;
using System.Collections.Generic;
using System.Linq;
using Fr.Sonic.Model.Config.FilterChain;
using SonicEddy.Contracts.DrumMixer;

namespace SonicEddy.Services.DrumMixer;

internal static class DrumMixerGraphBuilder
{
    internal const int ChannelCount = 16;
    internal const string Fil4Uri = "http://gareus.org/oss/lv2/fil4#stereo";
    internal const string TransientUri =
        "http://calf.sourceforge.net/plugins/TransientDesigner";
    internal const string CompressorUri =
        "http://calf.sourceforge.net/plugins/Compressor";
    internal const string SaturatorUri =
        "http://calf.sourceforge.net/plugins/Saturator";
    internal const string RoomUri = "urn:dragonfly:room";
    internal const string PlateUri = "urn:dragonfly:plate";

    internal static readonly string[] RequiredPluginUris =
    [
        Fil4Uri, TransientUri, CompressorUri, SaturatorUri, RoomUri, PlateUri
    ];

    public static FilterGraphConfig Build(DrumMixerConfig state)
    {
        List<FilterGraphNode> nodes = [];
        List<FilterGraphLink> links = [];
        List<string> inputs = [];

        var dryLeft = new string[ChannelCount];
        var dryRight = new string[ChannelCount];
        var roomLeft = new string[ChannelCount];
        var roomRight = new string[ChannelCount];
        var plateLeft = new string[ChannelCount];
        var plateRight = new string[ChannelCount];

        for (var i = 0; i < ChannelCount; i++)
        {
            var number = i + 1;
            var prefix = $"ch{number:00}";
            var channel = state.Channels[i];
            var copy = $"{prefix}_copy";
            var fil4 = $"{prefix}_fil4";
            var transient = $"{prefix}_transient";
            var compressor = $"{prefix}_compressor";
            var saturator = $"{prefix}_saturator";
            var splitLeft = $"{prefix}_split_l";
            var splitRight = $"{prefix}_split_r";

            nodes.Add(Builtin(copy, "copy"));
            nodes.Add(Lv2(fil4, Fil4Uri, Controls(channel, "fil4",
                new() { ["enable"] = 0.0 })));
            nodes.Add(Lv2(transient, TransientUri, Controls(channel,
                "transient", new() { ["bypass"] = 1.0 })));
            nodes.Add(Lv2(compressor, CompressorUri, Controls(channel,
                "compressor", new() { ["bypass"] = 1.0 })));
            nodes.Add(Lv2(saturator, SaturatorUri, Controls(channel,
                "saturator", new() { ["bypass"] = 1.0 })));
            nodes.Add(Builtin(splitLeft, "copy"));
            nodes.Add(Builtin(splitRight, "copy"));

            inputs.Add($"{copy}:In");
            Link(links, $"{copy}:Out", $"{fil4}:inL");
            Link(links, $"{copy}:Out", $"{fil4}:inR");
            LinkStereo(links, fil4, "outL", "outR", transient, "in_l", "in_r");
            LinkStereo(links, transient, "out_l", "out_r", compressor, "in_l",
                "in_r");
            LinkStereo(links, compressor, "out_l", "out_r", saturator, "in_l",
                "in_r");
            Link(links, $"{saturator}:out_l", $"{splitLeft}:In");
            Link(links, $"{saturator}:out_r", $"{splitRight}:In");

            dryLeft[i] = $"{splitLeft}:Out";
            roomLeft[i] = $"{splitLeft}:Out";
            plateLeft[i] = $"{splitLeft}:Out";
            dryRight[i] = $"{splitRight}:Out";
            roomRight[i] = $"{splitRight}:Out";
            plateRight[i] = $"{splitRight}:Out";
        }

        var dryL = AddBus(nodes, links, "dry_l", dryLeft,
            state.Channels.Select(c => c.Volume).ToArray());
        var dryR = AddBus(nodes, links, "dry_r", dryRight,
            state.Channels.Select(c => c.Volume).ToArray());
        var roomL = AddBus(nodes, links, "room_l", roomLeft,
            state.Channels.Select(c => c.RoomSend).ToArray());
        var roomR = AddBus(nodes, links, "room_r", roomRight,
            state.Channels.Select(c => c.RoomSend).ToArray());
        var plateL = AddBus(nodes, links, "plate_l", plateLeft,
            state.Channels.Select(c => c.PlateSend).ToArray());
        var plateR = AddBus(nodes, links, "plate_r", plateRight,
            state.Channels.Select(c => c.PlateSend).ToArray());

        nodes.Add(Lv2("room_reverb", RoomUri,
            ReturnControls(state.Room, new() { ["dry_level"] = 0.0 })));
        nodes.Add(Lv2("plate_reverb", PlateUri,
            ReturnControls(state.Plate, new() { ["dry_level"] = 0.0 })));
        Link(links, roomL, "room_reverb:lv2_audio_in_1");
        Link(links, roomR, "room_reverb:lv2_audio_in_2");
        Link(links, plateL, "plate_reverb:lv2_audio_in_1");
        Link(links, plateR, "plate_reverb:lv2_audio_in_2");

        nodes.Add(Mixer("final_l",
            [1.0f, state.Room.Volume, state.Plate.Volume]));
        nodes.Add(Mixer("final_r",
            [1.0f, state.Room.Volume, state.Plate.Volume]));
        Link(links, dryL, "final_l:In 1");
        Link(links, "room_reverb:lv2_audio_out_1", "final_l:In 2");
        Link(links, "plate_reverb:lv2_audio_out_1", "final_l:In 3");
        Link(links, dryR, "final_r:In 1");
        Link(links, "room_reverb:lv2_audio_out_2", "final_r:In 2");
        Link(links, "plate_reverb:lv2_audio_out_2", "final_r:In 3");

        return new()
        {
            Nodes = nodes,
            Links = links,
            Inputs = inputs,
            Outputs = ["final_l:Out", "final_r:Out"]
        };
    }

    private static string AddBus(List<FilterGraphNode> nodes,
        List<FilterGraphLink> links, string name, string[] sources,
        float[] gains)
    {
        var first = $"{name}_1_8";
        var second = $"{name}_9_16";
        var sum = $"{name}_sum";
        nodes.Add(Mixer(first, gains[..8]));
        nodes.Add(Mixer(second, gains[8..]));
        nodes.Add(Mixer(sum, [1.0f, 1.0f]));

        for (var i = 0; i < 8; i++)
        {
            Link(links, sources[i], $"{first}:In {i + 1}");
            Link(links, sources[i + 8], $"{second}:In {i + 1}");
        }

        Link(links, $"{first}:Out", $"{sum}:In 1");
        Link(links, $"{second}:Out", $"{sum}:In 2");
        return $"{sum}:Out";
    }

    private static FilterGraphNode Builtin(string name, string label) => new()
    {
        Name = name,
        Type = "builtin",
        Label = label
    };

    private static FilterGraphNode Mixer(string name, IReadOnlyList<float> gains)
    {
        var controls = gains.Select((gain, i) =>
                new KeyValuePair<string, object>($"Gain {i + 1}", (double)gain))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return new()
        {
            Name = name,
            Type = "builtin",
            Label = "mixer",
            Control = controls
        };
    }

    private static FilterGraphNode Lv2(string name, string uri,
        Dictionary<string, object> controls) => new()
    {
        Name = name,
        Type = "lv2",
        Plugin = uri,
        Control = controls
    };

    private static Dictionary<string, object> Controls(
        DrumMixerChannelConfig channel, string plugin,
        Dictionary<string, object> defaults)
    {
        foreach (var parameter in channel.Parameters.Where(p =>
                     p.Name.StartsWith($"{plugin}:", StringComparison.Ordinal)))
            defaults[parameter.Name[(plugin.Length + 1)..]] =
                (double)parameter.Value;
        return defaults;
    }

    private static Dictionary<string, object> ReturnControls(
        DrumMixerReturnConfig state, Dictionary<string, object> defaults)
    {
        foreach (var parameter in state.Parameters)
            defaults[parameter.Name] = (double)parameter.Value;
        return defaults;
    }

    private static void Link(List<FilterGraphLink> links, string output,
        string input) => links.Add(new() { Output = output, Input = input });

    private static void LinkStereo(List<FilterGraphLink> links, string output,
        string outputLeft, string outputRight, string input, string inputLeft,
        string inputRight)
    {
        Link(links, $"{output}:{outputLeft}", $"{input}:{inputLeft}");
        Link(links, $"{output}:{outputRight}", $"{input}:{inputRight}");
    }
}
