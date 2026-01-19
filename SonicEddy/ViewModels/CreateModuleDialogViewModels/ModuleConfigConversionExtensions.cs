using System;
using System.Collections.Generic;
using System.Linq;
using Fr.Wireplumber.Model.Config;
using Fr.Wireplumber.Model.Config.FilterChain;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Conversions;

namespace SonicEddy.ViewModels.CreateModuleDialogViewModels;

public static class ModuleConfigConversionExtensions
{
    public static FilterChainModuleConfig ToFilterChainConfig(
        this CreateModuleDialogViewModel viewModel, FilterGraph filterGraph)
    {
        var output = filterGraph.Nodes.OfType<FilterGraphOutput>().First();
        var input = filterGraph.Nodes.OfType<FilterGraphInput>().First();

        return new()
        {
            CaptureProps =
                viewModel.CaptureProps.ToNodeProperties(input.OutputPorts
                    .Select(p => p.Name).ToList()),
            PlaybackProps =
                viewModel.PlaybackProps.ToNodeProperties(output.InputPorts
                    .Select(p => p.Name).ToList()),
            FilterGraph = filterGraph.ToFilterGraphConfig()
        };
    }

    private static NodePropertiesConfig ToNodeProperties(
        this NodePropertiesViewModel nodeProps, List<string> audioPositions) =>
        new()
        {
            Name = nodeProps.Name,
            Description = nodeProps.Description,
            Linger = nodeProps.Linger,
            AutoConnect = nodeProps.AutoConnect,
            DontFallback = nodeProps.DontFallback,
            Passive = false,
            TargetObject = nodeProps.TargetObject?.Name,
            MediaClass = nodeProps.MediaClass,
            AudioPosition = audioPositions
        };
}