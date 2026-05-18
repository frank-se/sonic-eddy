using System.Collections.Concurrent;
using Fr.Sonic.Helper;
using Fr.Sonic.Model.Config.FilterChain;
using Fr.Sonic.Model.Config.LoopbackModule;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Modules.Models.Pending;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Registries.Modules;

namespace Fr.Sonic.Factories.Implementation;

internal class ModuleFactory(ModuleRegistry moduleRegistry) : IModuleFactory
{
    private readonly ConcurrentDictionary<string, PendingModule>
        _pendingModules = [];

    internal void UpdateNodesForWaitingModules(Node node)
    {
        if (node.Pmx.Tag is null || node.Pmx.Purpose is null) return;

        if (!_pendingModules.TryGetValue(node.Pmx.Tag, out var pending)) return;

        switch (pending)
        {
            case PendingTwoNodeModule pendingTwoNodeModule:
                switch (node.Pmx.Purpose)
                {
                    case PmxConstants.PurposeFilterChainCapture:
                    case PmxConstants.PurposeLoopbackCapture:
                        UpdateCaptureNodeData(node, pendingTwoNodeModule);
                        break;
                    case PmxConstants.PurposeFilterChainPlayback:
                    case PmxConstants.PurposeLoopbackPlayback:
                        UpdatePlaybackNodeData(node, pendingTwoNodeModule);
                        break;
                }

                break;
        }

        if (pending.TryComplete())
        {
            _pendingModules.TryRemove(node.Pmx.Tag, out _);
        }
    }

    private static void UpdateCaptureNodeData(Node node,
        PendingTwoNodeModule pending) => pending.SetCaptureNode(node);

    private static void UpdatePlaybackNodeData(Node node,
        PendingTwoNodeModule pending) => pending.SetPlaybackNode(node);

    public Task<LoopbackModule> CreateLoopbackModuleAsync(string name,
        LoopbackModuleConfig config) =>
        CreateTwoNodeModuleAsync<LoopbackModule, LoopbackModuleConfig>(
            "libpipewire-module-loopback", config, (moduleTag, moduleHandle,
                captureNode,
                playbackNode) => new(name, moduleTag, moduleHandle,
                captureNode,
                playbackNode),
            (moduleConfig, tag) =>
            {
                moduleConfig.CaptureProps.Tag = tag;
                moduleConfig.PlaybackProps.Tag = tag;
                moduleConfig.CaptureProps.Purpose =
                    PmxConstants.PurposeLoopbackCapture;
                moduleConfig.PlaybackProps.Purpose =
                    PmxConstants.PurposeLoopbackPlayback;
            });

    public Task<FilterChain> CreateFilterChainAsync(string name,
        FilterChainModuleConfig config) =>
        CreateTwoNodeModuleAsync<FilterChain, FilterChainModuleConfig>(
            "libpipewire-module-filter-chain", config,
            (moduleTag, moduleHandle, captureNode,
                playbackNode) => new(name, moduleTag, moduleHandle,
                captureNode,
                playbackNode),
            (moduleConfig, tag) =>
            {
                moduleConfig.CaptureProps.Tag = tag;
                moduleConfig.CaptureProps.Purpose =
                    PmxConstants.PurposeFilterChainCapture;
                moduleConfig.PlaybackProps.Tag = tag;
                moduleConfig.PlaybackProps.Purpose =
                    PmxConstants.PurposeFilterChainPlayback;
            }
        );

    private Task<T> CreateTwoNodeModuleAsync<T, TC>(string moduleName,
        TC config,
        Func<string, IntPtr, ulong, ulong, T> creator,
        Action<TC, string> tagAndPurposeInjector)
        where TC : class
        where T : TwoNodePipewireModule
    {
        var tag = TagHelper.GenerateTag();

        var pending =
            new PendingTwoNodeModuleWithTaskHandling<T>(tag, creator,
                moduleRegistry);

        if (!_pendingModules.TryAdd(tag, pending))
            throw new InvalidOperationException("Tag collision!");

        tagAndPurposeInjector(config, tag);

        return Task.Run(LoadAndWait);

        Task<T> LoadAndWait()
        {
            if (!FrSonicLib.LoadModuleC(moduleName,
                    config.SerializeConfig(), out var moduleHandle))
            {
                _pendingModules.TryRemove(tag, out _);
                var exception =
                    new InvalidOperationException("Couldn't load module");
                pending.FinishWithException(exception);
                throw exception;
            }

            pending.ModuleHandle = moduleHandle;
            return pending.GetTask();
        }
    }
}