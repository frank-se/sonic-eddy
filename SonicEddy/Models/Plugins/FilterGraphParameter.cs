using System;

namespace SonicEddy.Models.Plugins;

public class FilterGraphParameter(
    string name,
    string description,
    float @default,
    float minimum,
    float maximum,
    bool isLogarithmic,
    bool isInteger,
    bool isToggle,
    float normalizedValue)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public float Default { get; } = @default;
    public float Minimum { get; } = minimum;
    public float Maximum { get; } = maximum;
    public bool IsLogarithmic { get; } = isLogarithmic;
    public bool IsInteger { get; } = isInteger;
    public bool IsToggle { get; } = isToggle;
    public float NormalizedValue { get; set; } = normalizedValue;

    public event Action<float> ValueChanged;
}