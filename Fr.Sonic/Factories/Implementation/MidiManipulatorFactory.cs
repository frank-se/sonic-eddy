using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Fr.Sonic.Helper;
using Fr.Sonic.Model.Config.MidiManipulator;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Modules.Models.Pending;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Registries.Modules;

namespace Fr.Sonic.Factories.Implementation;

internal class MidiManipulatorFactory(ModuleRegistry moduleRegistry)
    : IMidiManipulatorFactory
{
    private readonly ConcurrentDictionary<string, PendingModule>
        _pendingManipulators = [];

    internal void UpdateNodesForWaitingManipulators(Node node)
    {
        if (node.Pmx.Tag is null || node.Pmx.Purpose is null) return;

        if (!_pendingManipulators.TryGetValue(node.Pmx.Tag, out var pending))
            return;

        if (pending is PendingTwoNodeModule pendingTwoNodeModule)
        {
            switch (node.Pmx.Purpose)
            {
                case PmxConstants.PurposeMidiManipulatorCapture:
                    pendingTwoNodeModule.SetCaptureNode(node);
                    break;
                case PmxConstants.PurposeMidiManipulatorPlayback:
                    pendingTwoNodeModule.SetPlaybackNode(node);
                    break;
            }
        }

        if (pending.TryComplete())
            _pendingManipulators.TryRemove(node.Pmx.Tag, out _);
    }

    public Task<MidiManipulator> CreateMidiManipulatorAsync(
        MidiManipulatorConfig config)
    {
        var tag = TagHelper.GenerateTag();
        UIntPtr handle = UIntPtr.Zero;

        var pending =
            new PendingTwoNodeModuleWithTaskHandling<MidiManipulator>(tag,
                (moduleTag, _, captureNode, playbackNode) =>
                    new(config.Name, moduleTag, handle, captureNode,
                        playbackNode),
                moduleRegistry);

        if (!_pendingManipulators.TryAdd(tag, pending))
            throw new InvalidOperationException("Tag collision!");

        return Task.Run(CreateAndWait);

        Task<MidiManipulator> CreateAndWait()
        {
            using var name = NativeUtf8String.From(config.Name);
            using var tagString = NativeUtf8String.From(tag);
            using var description = NativeUtf8String.From(config.Description);

            var nativeConfig = new FrSonicMidiManipulatorConfig
            {
                Name = name.Pointer,
                Tag = tagString.Pointer,
                Description = description.Pointer,
            };

            if (!FrSonicLib.CreateMidiManipulatorC(nativeConfig, out handle))
            {
                _pendingManipulators.TryRemove(tag, out _);
                var exception = new InvalidOperationException(
                    "Couldn't create MIDI manipulator");
                pending.FinishWithException(exception);
                throw exception;
            }

            pending.ModuleHandle = unchecked((IntPtr)(nuint)handle);
            return pending.GetTask();
        }
    }

    private sealed class NativeUtf8String : IDisposable
    {
        private NativeUtf8String(IntPtr pointer) => Pointer = pointer;

        internal IntPtr Pointer { get; private set; }

        internal static NativeUtf8String From(string value) =>
            new(Marshal.StringToCoTaskMemUTF8(value));

        public void Dispose()
        {
            if (Pointer == IntPtr.Zero) return;
            Marshal.FreeCoTaskMem(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
