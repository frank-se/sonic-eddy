using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Wireplumber.Model.Config.FilterChain;
using Fr.Wireplumber.Model.Params;
using Fr.Wireplumber.Model.PropInfo;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Conversions;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViews;

namespace SonicEddy.ViewModels.MixerViewModels;

public class ChannelStripViewModel : ReactiveObject
{
    public ChannelStripViewModel(IAppDataService appDataService,
        ulong channelId)
    {
        _appDataService = appDataService;
        _channelId = channelId;
    }

    private readonly ulong _channelId;

    public float Volume
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1.0f;

    public float Pan
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 0.0f;

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
                    Name = $"mixer-fc-{_channelId}-capture",
                    Description =
                        $"Capture Node for Mixer Filter Channel {_channelId}",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = null,
                    MediaClass = "Stream/Input/Audio",
                    AudioPosition = ["FL", "FR"]
                },
                PlaybackProps = new()
                {
                    Name = $"mixer-fc-{_channelId}-playback",
                    Description =
                        $"Playback Node for Mixer Filter Channel {_channelId}",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = null,
                    MediaClass = "Stream/Output/Audio",
                    AudioPosition = ["FL", "FR"]
                },
                FilterGraph = dialogViewModel.SelectedFilterGraph
                    .ToFilterGraphConfig()
            };

            _filterChain =
                await Fr.Wireplumber.Wireplumber.ModuleFactory
                    .CreateFilterChainAsync(
                        $"mixer-fc-{_channelId}", filterChainConfig);

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
                    PluginName = parameter.Key.Split(":")[0]
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
                new ParameterViewModel(filterChain.CaptureNode)
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
    }

    private readonly IAppDataService _appDataService;
}