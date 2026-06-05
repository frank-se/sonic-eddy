using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Fr.Sonic.Helper;
using Fr.Sonic.Model.Config.ClickSync;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Modules.Models.Pending;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Registries.Modules;

namespace Fr.Sonic.Factories.Implementation;

internal sealed class ClickSyncConverterFactory(ModuleRegistry moduleRegistry)
    : IClickSyncConverterFactory
{
    private readonly ConcurrentDictionary<string, PendingModule>
        _pendingConverters = [];

    internal void UpdateNodesForWaitingConverters(Node node)
    {
        if (node.Pmx.Tag is null || node.Pmx.Purpose is null) return;
        if (!_pendingConverters.TryGetValue(node.Pmx.Tag, out var pending))
            return;

        if (pending is PendingClickSyncConverter converter)
        {
            switch (node.Pmx.Purpose)
            {
                case PmxConstants.PurposeClickSyncClick:
                    converter.SetClickNode(node);
                    break;
                case PmxConstants.PurposeClickSyncReset:
                    converter.SetResetNode(node);
                    break;
                case PmxConstants.PurposeClickSyncRun:
                    converter.SetRunNode(node);
                    break;
            }
        }

        if (pending.TryComplete())
            _pendingConverters.TryRemove(node.Pmx.Tag, out _);
    }

    public Task<ClickSyncConverter> CreateClickSyncConverterAsync(
        ClickSyncConverterConfig config)
    {
        var tag = TagHelper.GenerateTag();
        UIntPtr handle = UIntPtr.Zero;
        var pending = new PendingClickSyncConverter(tag,
            (moduleTag, moduleHandle, clickNode, resetNode, runNode) =>
                new(config.Name, moduleTag,
                    unchecked((UIntPtr)(nuint)moduleHandle), clickNode,
                    resetNode, runNode),
            moduleRegistry);

        if (!_pendingConverters.TryAdd(tag, pending))
            throw new InvalidOperationException("Tag collision!");

        return Task.Run(CreateAndWait);

        Task<ClickSyncConverter> CreateAndWait()
        {
            using var id = NativeUtf8String.From(config.Id.ToString("D"));
            using var name = NativeUtf8String.From(config.Name);
            using var tagString = NativeUtf8String.From(tag);
            var nativeConfig = new FrSonicClickSyncConfig
            {
                Id = id.Pointer,
                Name = name.Pointer,
                Tag = tagString.Pointer,
                PulsesPerQuarterNote = config.PulsesPerQuarterNote,
                PulseLengthMs = config.PulseLengthMs,
                PulseAmplitude = config.PulseAmplitude
            };

            if (!FrSonicLib.CreateClickSyncConverterC(nativeConfig, out handle))
            {
                _pendingConverters.TryRemove(tag, out _);
                var exception = new InvalidOperationException(
                    "Couldn't create click sync converter");
                pending.FinishWithException(exception);
                throw exception;
            }

            pending.SetModuleHandle(unchecked((IntPtr)(nuint)handle));
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

        public void Dispose()
        {
            if (Pointer == IntPtr.Zero) return;
            Marshal.FreeCoTaskMem(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
