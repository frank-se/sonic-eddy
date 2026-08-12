using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MixerServiceV2;

public record Mixer(MixerLayer[] Layers, LoopbackModule? MonitoringLoopback = null, GlobalMasterChannel? GlobalMaster = null, CueChannel? Cue = null, MicChannel? Mic = null, MicChannel? Mic2 = null);