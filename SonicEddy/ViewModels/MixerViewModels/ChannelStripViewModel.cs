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
using SonicEddy.Services.MixerData;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViews;
using AddFilterChainView = SonicEddy.Views.MixerViewsV2.AddFilterChainView;

namespace SonicEddy.ViewModels.MixerViewModels;

public class ChannelStripViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public Node PlaybackNode { get; }

    public PanAndVolumeViewModel PanAndVolumeViewModel { get; }

    private readonly LoopbackModule _loopbackModule;

    private readonly IMixerService _mixerService;

    public ChannelStripViewModel(IAppDataService appDataService,
        ulong channelId, Node playbackNode, FilterChain? filterChain,
        LoopbackModule loopbackModule, IMixerService mixerService)
    {
        _loopbackModule = loopbackModule;
        _appDataService = appDataService;
        ChannelId = channelId;
        PlaybackNode = playbackNode;
        PanAndVolumeViewModel = new(loopbackModule);
        _mixerService = mixerService;
        
        if (filterChain is null) return;
        _filterChain = filterChain;
        _ = Task.Run(async () => await AddPluginsFromFilterChain(_filterChain));
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
            var channelStrip = await _mixerService.AddFilterToChannelStrip(ChannelId,
                dialogViewModel.SelectedFilterGraph);
            _filterChain = channelStrip.FilterModule;
            
            if (_filterChain is not null)
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