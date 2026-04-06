using NSubstitute;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels.CreateModuleDialogViewModels;

namespace SonicEddy.Tests.DataMapping.CreateModuleDialogViewModels;

public class FilterChainModuleConfigTests
{
    [Fact]
    public void CreateFilterChainModuleConfig()
    {
        List<Guid> outputNodePortIds = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> inputNodePortIds = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginOneInputPorts = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginOneOutputPorts = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginTwoInputPorts = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> pluginTwoOutputPorts = [Guid.NewGuid(), Guid.NewGuid()];

        var filterGraph = new FilterGraph(
            Guid.NewGuid(),
            "My Filter Graph",
            [
                new FilterGraphOutput(Guid.NewGuid(), [
                    new(outputNodePortIds[0], "FL"),
                    new(outputNodePortIds[1], "FR"),
                ]),
                new FilterGraphLv2Plugin(Guid.NewGuid(), "Compressor_1",
                    "http://calf.sourceforge.net/plugins/Compressor",
                    [
                        new(pluginOneInputPorts[0], "Input", "in_1"),
                        new(pluginOneInputPorts[1], "Input", "in_2"),
                    ],
                    [
                        new(pluginOneOutputPorts[0], "Output", "out_1"),
                        new(pluginOneOutputPorts[1], "Output", "out_2")
                    ]),
                new FilterGraphLv2Plugin(Guid.NewGuid(), "Compressor_2",
                    "http://calf.sourceforge.net/plugins/Compressor",
                    [
                        new(pluginTwoInputPorts[0], "Input", "in_1"),
                        new(pluginTwoInputPorts[1], "Input", "in_2"),
                    ],
                    [
                        new(pluginTwoOutputPorts[0], "Output", "out_1"),
                        new(pluginTwoOutputPorts[1], "Output", "out_2")
                    ]),
                new FilterGraphInput(Guid.NewGuid(), [
                    new(inputNodePortIds[0], "FL"),
                    new(inputNodePortIds[1], "FR")
                ])
            ],
            [
                new(inputNodePortIds[0], pluginOneInputPorts[0]),
                new(inputNodePortIds[1], pluginOneInputPorts[1]),
                new(pluginOneOutputPorts[0], pluginTwoInputPorts[0]),
                new(pluginOneOutputPorts[1], pluginTwoInputPorts[1]),
                new(pluginTwoOutputPorts[0], outputNodePortIds[0]),
                new(pluginTwoOutputPorts[1], outputNodePortIds[1])
            ], []);

        var appDataService = Substitute.For<IAppDataService>();
        appDataService.GetFilterGraph(filterGraph.Id)
            .Returns(Task.FromResult(filterGraph));

        var wireplumberService = Substitute.For<IWireplumberService>();
        wireplumberService.GetTargetObjectsForCaptureNode().Returns([]);
        wireplumberService.GetTargetObjectsForPlaybackNode().Returns([]);

        var viewModel = new CreateModuleDialogViewModel(
            appDataService, wireplumberService);

        viewModel.CaptureProps.Name = "Capture Name";
        viewModel.CaptureProps.Description = "Capture Description";
        viewModel.CaptureProps.AutoConnect = true;
        viewModel.CaptureProps.DontFallback = false;
        viewModel.CaptureProps.Linger = true;
        viewModel.CaptureProps.TargetObject = new()
        {
            Description = "test",
            Name = "node 1",
            ObjectSerial = 3
        };

        Assert.True(viewModel.CaptureProps.IsValid);

        viewModel.PlaybackProps.Name = "Playback Name";
        viewModel.PlaybackProps.Description = "Playback Description";
        viewModel.PlaybackProps.AutoConnect = false;
        viewModel.PlaybackProps.DontFallback = true;
        viewModel.PlaybackProps.Linger = false;
        viewModel.PlaybackProps.TargetObject = new()
        {
            Description = "test",
            Name = "node 2",
            ObjectSerial = 5
        };

        Assert.True(viewModel.PlaybackProps.IsValid);

        viewModel.SelectedModuleType = viewModel.SupportedModules.First();
        viewModel.SelectedFilterGraph = new()
        {
            Name = filterGraph.Name,
            Id = filterGraph.Id
        };

        var result = viewModel.ToFilterChainConfig(filterGraph);

        // Check Capture Props
        Assert.Equal(viewModel.CaptureProps.Name, result.CaptureProps.Name);
        Assert.Equal(viewModel.CaptureProps.Description,
            result.CaptureProps.Description);
        Assert.Equal(viewModel.CaptureProps.Linger, result.CaptureProps.Linger);
        Assert.Equal(viewModel.CaptureProps.AutoConnect,
            result.CaptureProps.AutoConnect);
        Assert.Equal(viewModel.CaptureProps.DontFallback,
            result.CaptureProps.DontFallback);
        Assert.Equal(false, result.CaptureProps.Passive);
        Assert.Equal(viewModel.CaptureProps.TargetObject.Name,
            result.CaptureProps.TargetObject);
        Assert.Equal(viewModel.CaptureProps.MediaClass,
            result.CaptureProps.MediaClass);
        Assert.Equal(["FL", "FR"], result.CaptureProps.AudioPosition);

        // Check Playback Props
        Assert.Equal(viewModel.PlaybackProps.Name, result.PlaybackProps.Name);
        Assert.Equal(viewModel.PlaybackProps.Description,
            result.PlaybackProps.Description);
        Assert.Equal(viewModel.PlaybackProps.Linger,
            result.PlaybackProps.Linger);
        Assert.Equal(viewModel.PlaybackProps.AutoConnect,
            result.PlaybackProps.AutoConnect);
        Assert.Equal(viewModel.PlaybackProps.DontFallback,
            result.PlaybackProps.DontFallback);
        Assert.Equal(false, result.PlaybackProps.Passive);
        Assert.Equal(viewModel.PlaybackProps.TargetObject.Name,
            result.PlaybackProps.TargetObject);
        Assert.Equal(viewModel.PlaybackProps.MediaClass,
            result.PlaybackProps.MediaClass);
        Assert.Equal(["FL", "FR"], result.PlaybackProps.AudioPosition);

        // Just spot checks for filter graph, was tested in its own test case
        Assert.NotNull(result.FilterGraph.Inputs);
        Assert.Equal(2, result.FilterGraph.Inputs.Count);

        Assert.NotNull(result.FilterGraph.Outputs);
        Assert.Equal(2, result.FilterGraph.Outputs.Count);

        Assert.Equal(2, result.FilterGraph.Nodes.Count);

        Assert.Equal(2, result.FilterGraph.Links.Count);
    }
}