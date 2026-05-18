using Fr.Sonic.Model;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Props;

namespace Fr.Sonic.Modules.Models;

/// <summary>
/// A loopback from the loopback module. The loopback module is created by
/// the Module Factory.
/// </summary>
/// <param name="Name"><inheritdoc cref="PipewireModule.Name" /></param>
/// <param name="Tag"><inheritdoc cref="PipewireModule.Tag" /></param>
/// <param name="ModuleHandle">
/// <inheritdoc cref="PipewireModule.ModuleHandle"/>
/// </param>
/// <param name="CaptureNodeObjectSerial">
/// <inheritdoc cref="TwoNodePipewireModule.CaptureNodeObjectSerial" />
/// </param>
/// <param name="PlaybackNodeObjectSerial">
/// <inheritdoc cref="TwoNodePipewireModule.PlaybackNodeObjectSerial" />
/// </param>
public record LoopbackModule(
    string Name,
    string Tag,
    IntPtr ModuleHandle,
    ulong CaptureNodeObjectSerial,
    ulong PlaybackNodeObjectSerial
) : TwoNodePipewireModule(Name, Tag, ModuleHandle, CaptureNodeObjectSerial,
    PlaybackNodeObjectSerial);