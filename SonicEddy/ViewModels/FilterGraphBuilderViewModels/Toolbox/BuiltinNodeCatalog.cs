using System;
using System.Collections.Generic;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;

public record BuiltinControlDef(string Name, double Default);

public record BuiltinNodeDefinition(
    BuiltinNodeType Type,
    string DisplayName,
    string PwName,
    bool HasVariableChannelCount,
    IReadOnlyList<BuiltinControlDef> DefaultControls);

public record BuiltinNodeGroup(string Name, IReadOnlyList<BuiltinNodeType> Types);

public static class BuiltinNodeCatalog
{
    private static readonly Dictionary<BuiltinNodeType, BuiltinNodeDefinition> Definitions =
        new()
        {
            [BuiltinNodeType.Mixer] = new(BuiltinNodeType.Mixer, "Mixer", "mixer", true, []),
            [BuiltinNodeType.Copy] = new(BuiltinNodeType.Copy, "Copy", "copy", false, []),
            [BuiltinNodeType.Invert] = new(BuiltinNodeType.Invert, "Invert", "invert", false, []),
            [BuiltinNodeType.Linear] = new(BuiltinNodeType.Linear, "Linear", "linear", false,
            [
                new("Mult", 1.0), new("Add", 0.0)
            ]),
            [BuiltinNodeType.Clamp] = new(BuiltinNodeType.Clamp, "Clamp", "clamp", false,
            [
                new("Min", -1.0), new("Max", 1.0)
            ]),
            [BuiltinNodeType.Reciprocal] = new(BuiltinNodeType.Reciprocal, "Reciprocal", "recip", false, []),
            [BuiltinNodeType.Abs] = new(BuiltinNodeType.Abs, "Abs", "abs", false, []),
            [BuiltinNodeType.Sqrt] = new(BuiltinNodeType.Sqrt, "Sqrt", "sqrt", false, []),
            [BuiltinNodeType.Exp] = new(BuiltinNodeType.Exp, "Exp", "exp", false,
            [
                new("Base", Math.E)
            ]),
            [BuiltinNodeType.Log] = new(BuiltinNodeType.Log, "Log", "log", false,
            [
                new("Base", 10.0), new("M1", 1.0), new("M2", 1.0)
            ]),
            [BuiltinNodeType.Multiply] = new(BuiltinNodeType.Multiply, "Multiply", "mult", true, []),
            [BuiltinNodeType.Sine] = new(BuiltinNodeType.Sine, "Sine", "sine", false,
            [
                new("Freq", 440.0), new("Ampl", 1.0), new("Offset", 0.0), new("Phase", 0.0)
            ]),
            [BuiltinNodeType.Max] = new(BuiltinNodeType.Max, "Max", "max", true, []),
            [BuiltinNodeType.DcBlock] = new(BuiltinNodeType.DcBlock, "DC Block", "dc_block", true,
            [
                new("R", 0.995)
            ]),
            [BuiltinNodeType.Ramp] = new(BuiltinNodeType.Ramp, "Ramp", "ramp", false,
            [
                new("Start", 0.0), new("Stop", 1.0), new("Duration (s)", 1.0)
            ]),
            [BuiltinNodeType.Debug] = new(BuiltinNodeType.Debug, "Debug", "debug", false, []),
            [BuiltinNodeType.ZeroRamp] = new(BuiltinNodeType.ZeroRamp, "Zero Ramp", "zeroramp", false,
            [
                new("Gap (s)", 0.000666), new("Duration (s)", 0.000666)
            ]),
            [BuiltinNodeType.NoiseGate] = new(BuiltinNodeType.NoiseGate, "Noise Gate", "noisegate", false,
            [
                new("Close threshold", 0.01), new("Open threshold", 0.02),
                new("Hold (s)", 0.1), new("Attack (s)", 0.01), new("Release (s)", 0.1)
            ]),
        };

    public static BuiltinNodeDefinition Get(BuiltinNodeType type) => Definitions[type];

    public static string ToPwName(this BuiltinNodeType type) => Definitions[type].PwName;

    public static readonly IReadOnlyList<BuiltinNodeGroup> Groups =
    [
        new("Routing",
        [
            BuiltinNodeType.Mixer, BuiltinNodeType.Copy,
            BuiltinNodeType.Multiply, BuiltinNodeType.Max
        ]),
        new("Math",
        [
            BuiltinNodeType.Linear, BuiltinNodeType.Clamp, BuiltinNodeType.Reciprocal,
            BuiltinNodeType.Abs, BuiltinNodeType.Sqrt, BuiltinNodeType.Exp, BuiltinNodeType.Log
        ]),
        new("Generators",   [BuiltinNodeType.Sine, BuiltinNodeType.Ramp]),
        new("Dynamics",
        [
            BuiltinNodeType.NoiseGate, BuiltinNodeType.ZeroRamp, BuiltinNodeType.DcBlock
        ]),
        new("Utility",      [BuiltinNodeType.Invert, BuiltinNodeType.Debug]),
    ];
}
