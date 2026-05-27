using ProtoBuf;

namespace SonicEddy.Contracts.FilterGraph;

[ProtoContract]
public enum BuiltinNodeType
{
    Mixer,
    Copy,
    Invert,
    Linear,
    Clamp,
    Reciprocal,
    Abs,
    Sqrt,
    Exp,
    Log,
    Multiply,
    Sine,
    Max,
    DcBlock,
    Ramp,
    Debug,
    ZeroRamp,
    NoiseGate
}
