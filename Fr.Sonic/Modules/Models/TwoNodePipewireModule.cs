using Fr.Sonic.Model;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Props;
using Fr.Sonic.Registries;

namespace Fr.Sonic.Modules.Models;

/// <summary>
/// A pipewire module that create two nodes
/// </summary>
/// <param name="Name"><see cref="PipewireModule.Name" /></param>
/// <param name="Tag">
/// <inheritdoc cref="PipewireModule.Tag" />
/// </param>
/// <param name="ModuleHandle">
/// <inheritdoc cref="PipewireModule.ModuleHandle"/>
/// </param>
/// <param name="CaptureNodeObjectSerial">
/// The object serial of the capture node.
/// </param>
/// <param name="PlaybackNodeObjectSerial">
/// The object serial of the playback node.
/// </param>
public abstract record TwoNodePipewireModule(
    string Name,
    string Tag,
    IntPtr ModuleHandle,
    ulong CaptureNodeObjectSerial,
    ulong PlaybackNodeObjectSerial)
    : PipewireModule(Name, Tag, ModuleHandle)
{
    /// <summary>
    /// The capture node
    /// </summary>
    public Node CaptureNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(
            CaptureNodeObjectSerial)!;

    /// <summary>
    /// The playback node
    /// </summary>
    public Node PlaybackNode =>
        FrSonic.NodeRegistry.GetByObjectSerial(
            PlaybackNodeObjectSerial)!;
}