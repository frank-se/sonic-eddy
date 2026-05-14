using Fr.Wireplumber.Model;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Registries;

namespace Fr.Wireplumber.Modules.Models;

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
        Wireplumber.NodeRegistry.GetByObjectSerial(
            CaptureNodeObjectSerial)!;

    /// <summary>
    /// The playback node
    /// </summary>
    public Node PlaybackNode =>
        Wireplumber.NodeRegistry.GetByObjectSerial(
            PlaybackNodeObjectSerial)!;
}