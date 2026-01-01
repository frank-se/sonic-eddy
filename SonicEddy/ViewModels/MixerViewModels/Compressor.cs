using ReactiveUI;
using SonicEddy.Models.Plugins;
using SonicEddy.Tools;

namespace SonicEddy.ViewModels.MixerViewModels;

public class Compressor : ReactiveObject
{
    private PipewireFilterGraph _pipewireFilterGraph;

    public BoundParameter Threshold { get; }

    public Compressor(PipewireFilterGraph pipewireFilterGraph)
    {
        _pipewireFilterGraph = pipewireFilterGraph;
        Threshold = new(_pipewireFilterGraph.Parameters["threshold"]);
    }
}