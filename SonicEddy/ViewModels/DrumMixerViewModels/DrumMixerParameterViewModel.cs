using System;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.DrumMixerViewModels;

public sealed class DrumMixerParameterViewModel(
    string name,
    string fullyQualifiedName,
    float minimum,
    float maximum,
    float initialValue,
    Action<float> changed) : ReactiveObject, IParameter
{
    private bool _initialized;

    public float Value
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (_initialized)
                changed(value);
        }
    } = initialValue;

    public float Minimum { get; } = minimum;
    public float Maximum { get; } = maximum;
    public string Name { get; } = name;
    public string FullyQualifiedName { get; } = fullyQualifiedName;
    public bool IsMainParameter => true;

    public void EnableChanges() => _initialized = true;
}
