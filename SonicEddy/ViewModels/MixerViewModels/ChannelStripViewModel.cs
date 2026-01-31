using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Wireplumber.Model.Config.FilterChain;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params;
using Fr.Wireplumber.Model.PropInfo;
using Fr.Wireplumber.Model.Props;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Conversions;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViews;

namespace SonicEddy.ViewModels.MixerViewModels;

/// <summary>
/// The channel strip view model controls a channel strip. A channel strip has
/// two possible setups, the first is:
///
/// <list type="bullet">
/// <item>
/// A playback node, the source for the channel strip, connected to
/// </item>
/// <item>
/// A loopback module, which provides the volume controls for the channel strip
/// </item>
/// </list>
///
/// The other option adds a filter chain in the signal path:
/// <list type="bullet">
/// <item>
/// A playback node, the source for the channel strip, connected to
/// </item>
/// <item>
/// A filter chain module, providing processing for the channel, connected to
/// </item>
/// <item>
/// A loopback module, which provides the volume controls for the channel strip
/// </item>
/// </list>
/// </summary>
public class ChannelStripViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public Node PlaybackNode { get; }

    public required ulong ObjectSerial { get; init; }

    public PanAndVolumeViewModel PanAndVolumeViewModel { get; }

    private readonly LoopbackModule _loopbackModule;

    public ChannelStripViewModel(IAppDataService appDataService,
        ulong channelId, Node playbackNode, LoopbackModule loopbackModule)
    {
        _loopbackModule = loopbackModule;
        _appDataService = appDataService;
        ChannelId = channelId;
        PlaybackNode = playbackNode;
        PanAndVolumeViewModel = new(loopbackModule);
    }

    public readonly ulong ChannelId;

    public ObservableCollection<PluginViewModel> Plugins { get; } = [];

    private FilterChain? _filterChain;

    public async Task SetFilterChain()
    {
        var dialogViewModel = new AddFilterChainViewModel(_appDataService);
        var dialog = new AddFilterChainView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel is
            { DialogResult: true, SelectedFilterGraph: not null })
        {
            var filterChainConfig = new FilterChainModuleConfig()
            {
                CaptureProps = new()
                {
                    Name = $"mixer-fc-{ChannelId}-capture",
                    Description =
                        $"Capture Node for Mixer Filter Channel {ChannelId}",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = PlaybackNode.ObjectSerial.ToString(),
                    MediaClass = "Stream/Input/Audio",
                    AudioPosition = ["FL", "FR"]
                },
                PlaybackProps = new()
                {
                    Name = $"mixer-fc-{ChannelId}-playback",
                    Description =
                        $"Playback Node for Mixer Filter Channel {ChannelId}",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = _loopbackModule.CaptureNode.ObjectSerial
                        .ToString(),
                    MediaClass = "Stream/Output/Audio",
                    AudioPosition = ["FL", "FR"]
                },
                FilterGraph = dialogViewModel.SelectedFilterGraph
                    .ToFilterGraphConfig()
            };

            _filterChain =
                await Fr.Wireplumber.Wireplumber.ModuleFactory
                    .CreateFilterChainAsync(
                        $"mixer-fc-{ChannelId}", filterChainConfig);

            _loopbackModule.CaptureNode.OverrideTargetObject(
                _filterChain.PlaybackNode.ObjectSerial.ToString());
            
            await AddPluginsFromFilterChain(_filterChain);
        }
    }

    private async Task AddPluginsFromFilterChain(FilterChain filterChain)
    {
        var parameters = await filterChain.CaptureNode.Params;
        var propInfos = await filterChain.CaptureNode.PropertyInfos;

        Dictionary<string, CollectedParams> paramsMap = [];

        foreach (var parameter in parameters!)
        {
            if (parameter.Value is Parameter<float> value &&
                parameter.Key.Contains(":"))
            {
                paramsMap[parameter.Key] = new()
                {
                    Parameter = value,
                    Name = parameter.Key.Split(":")[1],
                    PluginName = parameter.Key.Split(":")[0],
                    FullName = parameter.Key
                };
            }
        }

        foreach (var propInfo in propInfos.PropertyInfos)
        {
            if (propInfo is { PropertyType: FloatRange range, IsParam: true } &&
                propInfo.Name.Contains(":"))
            {
                if (paramsMap.TryGetValue(propInfo.Name, out var p))
                {
                    p.Range = range;
                }
            }
        }

        var groupedParams = paramsMap.Values.Where(p => p.Range is not null)
            .GroupBy(p => p.PluginName);

        var plugins = groupedParams.Select(g => new PluginViewModel()
        {
            Name = g.Key,
            Parameters = g.Select(p =>
                new ParameterViewModel(filterChain.CaptureNode, p.FullName)
                {
                    Name = p.Name,
                    Maximum = p.Range!.Maximum,
                    Minimum = p.Range.Minimum,
                    Value = p.Parameter.Value
                }).ToList()
        });

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Plugins.Clear();
            Plugins.AddRange(plugins);
        });
    }

    private class CollectedParams
    {
        public required Parameter<float> Parameter;
        public required string Name;
        public required string PluginName;
        public FloatRange? Range;
        public required string FullName;
    }

    private readonly IAppDataService _appDataService;

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}