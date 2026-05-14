using Fr.Wireplumber.Model;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Props;

namespace Fr.Wireplumber.Modules.Models;

/// <summary>
/// A filter chain from the filter chain module. The filter chain is created by
/// the Module Factory.
/// </summary>
/// <param name="Name"><inheritdoc cref="PipewireModule.Name" /></param>
/// <param name="Tag">
/// <inheritdoc cref="PipewireModule.Tag" />
/// </param>
/// <param name="ModuleHandle">
/// <inheritdoc cref="PipewireModule.ModuleHandle"/>
/// </param>
/// <param name="CaptureNodeObjectSerial">
/// <inheritdoc cref="TwoNodePipewireModule.CaptureNodeObjectSerial" />
/// </param>
/// <param name="PlaybackNodeObjectSerial">
/// <inheritdoc cref="TwoNodePipewireModule.PlaybackNodeObjectSerial" />
/// </param>
public record FilterChain(
    string Name,
    string Tag,
    IntPtr ModuleHandle,
    ulong CaptureNodeObjectSerial,
    ulong PlaybackNodeObjectSerial
) : TwoNodePipewireModule(Name, Tag, ModuleHandle, CaptureNodeObjectSerial,
    PlaybackNodeObjectSerial);