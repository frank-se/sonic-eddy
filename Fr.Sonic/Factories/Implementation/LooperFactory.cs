using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Fr.Sonic.Helper;
using Fr.Sonic.Model.Config.Looper;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Modules.Models.Pending;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Registries.Modules;

namespace Fr.Sonic.Factories.Implementation;

internal class LooperFactory(ModuleRegistry moduleRegistry) : ILooperFactory
{
    private readonly ConcurrentDictionary<string, PendingModule>
        _pendingLoopers = [];

    internal void UpdateNodesForWaitingLoopers(Node node)
    {
        if (node.Pmx.Tag is null || node.Pmx.Purpose is null) return;

        if (!_pendingLoopers.TryGetValue(node.Pmx.Tag, out var pending)) return;

        if (pending is PendingTwoNodeModule pendingTwoNodeModule)
        {
            switch (node.Pmx.Purpose)
            {
                case PmxConstants.PurposeLooperCapture:
                    pendingTwoNodeModule.SetCaptureNode(node);
                    break;
                case PmxConstants.PurposeLooperPlayback:
                    pendingTwoNodeModule.SetPlaybackNode(node);
                    break;
            }
        }

        if (pending.TryComplete())
            _pendingLoopers.TryRemove(node.Pmx.Tag, out _);
    }

    public Task<Looper> CreateLooperAsync(LooperConfig config)
    {
        var tag = TagHelper.GenerateTag();
        UIntPtr looperHandle = UIntPtr.Zero;

        var pending =
            new PendingTwoNodeModuleWithTaskHandling<Looper>(tag,
                (moduleTag, moduleHandle, captureNode, playbackNode) =>
                    new(config.Name, moduleTag,
                        unchecked((UIntPtr)(nuint)moduleHandle), captureNode,
                        playbackNode),
                moduleRegistry);

        if (!_pendingLoopers.TryAdd(tag, pending))
            throw new InvalidOperationException("Tag collision!");

        return Task.Run(CreateAndWait);

        Task<Looper> CreateAndWait()
        {
            using var name = NativeUtf8String.From(config.Name);
            using var tagString = NativeUtf8String.From(tag);
            using var description = NativeUtf8String.From(config.Description);
            using var captureTarget =
                NativeUtf8String.FromNullable(config.CaptureTargetObject);
            using var playbackTarget =
                NativeUtf8String.FromNullable(config.PlaybackTargetObject);
            using var archiveFolderPath =
                NativeUtf8String.FromNullable(config.ArchiveFolderPath);
            using var playbackAudioPosition =
                NativeUtf8String.FromNullable(config.PlaybackAudioPosition);

            var nativeConfig = new FrSonicLooperConfig
            {
                Name = name.Pointer,
                Tag = tagString.Pointer,
                Description = description.Pointer,
                CaptureTargetObject = captureTarget.Pointer,
                PlaybackTargetObject = playbackTarget.Pointer,
                ArchiveFolderPath = archiveFolderPath.Pointer,
                Channels = config.Channels,
                MaxRecordSeconds = config.MaxRecordSeconds,
                Mix = Math.Clamp(config.Mix, 0.0f, 1.0f),
                PlaybackAudioPosition = playbackAudioPosition.Pointer,
            };

            if (!FrSonicLib.CreateLooperC(nativeConfig, out looperHandle))
            {
                _pendingLoopers.TryRemove(tag, out _);
                var exception =
                    new InvalidOperationException("Couldn't create looper");
                pending.FinishWithException(exception);
                throw exception;
            }

            pending.SetModuleHandle(unchecked((IntPtr)(nuint)looperHandle));
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
