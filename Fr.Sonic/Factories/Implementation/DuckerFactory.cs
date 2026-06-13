using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Fr.Sonic.Helper;
using Fr.Sonic.Model.Config.Ducker;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Modules.Models.Pending;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Registries.Modules;

namespace Fr.Sonic.Factories.Implementation;

internal class DuckerFactory(ModuleRegistry moduleRegistry) : IDuckerFactory
{
    private readonly ConcurrentDictionary<string, PendingModule>
        _pendingDuckers = [];

    internal void UpdateNodesForWaitingDuckers(Node node)
    {
        if (node.Pmx.Tag is null || node.Pmx.Purpose is null) return;
        if (!_pendingDuckers.TryGetValue(node.Pmx.Tag, out var pending)) return;

        if (pending is PendingThreeNodeModule threeNode)
        {
            switch (node.Pmx.Purpose)
            {
                case PmxConstants.PurposeDuckerMidi:
                    threeNode.SetFirstNode(node);
                    break;
                case PmxConstants.PurposeDuckerCapture:
                    threeNode.SetSecondNode(node);
                    break;
                case PmxConstants.PurposeDuckerPlayback:
                    threeNode.SetThirdNode(node);
                    break;
            }
        }

        if (pending.TryComplete())
            _pendingDuckers.TryRemove(node.Pmx.Tag, out _);
    }

    public Task<Ducker> CreateDuckerAsync(DuckerConfig config)
    {
        var tag = TagHelper.GenerateTag();
        UIntPtr duckerHandle = UIntPtr.Zero;

        var pending = new PendingThreeNodeModuleWithTaskHandling<Ducker>(
            tag,
            (moduleTag, moduleHandle, midiSerial, captureSerial, playbackSerial) =>
                new(config.Name, moduleTag,
                    unchecked((UIntPtr)(nuint)moduleHandle),
                    midiSerial, captureSerial, playbackSerial),
            moduleRegistry);

        if (!_pendingDuckers.TryAdd(tag, pending))
            throw new InvalidOperationException("Tag collision!");

        return Task.Run(CreateAndWait);

        Task<Ducker> CreateAndWait()
        {
            using var name = NativeUtf8String.From(config.Name);
            using var tagString = NativeUtf8String.From(tag);
            using var description = NativeUtf8String.From(config.Description);
            using var captureTarget = NativeUtf8String.FromNullable(config.CaptureTargetObject);
            using var playbackTarget = NativeUtf8String.FromNullable(config.PlaybackTargetObject);

            var nativeConfig = new FrSonicDuckerConfig
            {
                Name = name.Pointer,
                Tag = tagString.Pointer,
                Description = description.Pointer,
                CaptureTargetObject = captureTarget.Pointer,
                PlaybackTargetObject = playbackTarget.Pointer,
            };

            if (!FrSonicLib.CreateDuckerC(nativeConfig, out duckerHandle))
            {
                _pendingDuckers.TryRemove(tag, out _);
                var exception =
                    new InvalidOperationException("Couldn't create ducker");
                pending.FinishWithException(exception);
                throw exception;
            }

            pending.SetModuleHandle(unchecked((IntPtr)(nuint)duckerHandle));
            pending.TryComplete();
            return pending.GetTask();
        }
    }

    private sealed class NativeUtf8String : IDisposable
    {
        private NativeUtf8String(IntPtr pointer) => Pointer = pointer;
        internal IntPtr Pointer { get; private set; }

        internal static NativeUtf8String From(string value) =>
            new(Marshal.StringToCoTaskMemUTF8(value));

        internal static NativeUtf8String FromNullable(string? value) =>
            value is null ? new(IntPtr.Zero) : From(value);

        public void Dispose()
        {
            if (Pointer == IntPtr.Zero) return;
            Marshal.FreeCoTaskMem(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
