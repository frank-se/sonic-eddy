using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class ParameterViewModel : ReactiveObject
{
    public float Minimum { get; init; }
    public float Maximum { get; init; }
    public required string Name { get; init; }

    public float Value
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}