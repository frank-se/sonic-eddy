using System;
using System.Collections.Generic;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params;
using ReactiveUI;
using IParameter = SonicEddy.Controls.MixerControls.IParameter;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class ParameterViewModel : ReactiveObject, IParameter, IDisposable
{
    private readonly string _fullName;
    private readonly Node _node;

    public ParameterViewModel(float minimum, float maximum, string name,
        bool isMainParameter, string fullName, Node node)
    {
        Minimum = minimum;
        Maximum = maximum;
        Name = name;
        IsMainParameter = isMainParameter;

        _fullName = fullName;
        _node = node;

        node.ParamsChanged += OnParameterChanged;
    }

    private void OnParameterChanged(
        Dictionary<string, Fr.Wireplumber.Model.Params.IParameter>?
            parameters)
    {
        if (!(parameters?.TryGetValue(_fullName, out var value) ?? false))
            return;

        if (value is Parameter<float> floatValue)
            Value = floatValue.Value;
    }

    public float Value
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float Minimum { get; }
    public float Maximum { get; }
    public string Name { get; }
    public bool IsMainParameter { get; }

    public void Dispose()
    {
        _node.ParamsChanged -= OnParameterChanged;
        GC.SuppressFinalize(this);
    }
}