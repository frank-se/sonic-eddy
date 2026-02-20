using ReactiveUI;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.CustomControlTesterViewModels;

public class TestParameter : ReactiveObject, IParameter
{
    public float Value
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float Minimum { get; } = 0;
    public float Maximum { get; } = 10;
    public required string Name { get; init; }
    public required bool IsMainParameter { get; init; }
}