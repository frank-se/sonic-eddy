using System;
using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using ReactiveUI;
using IParameter = SonicEddy.Controls.MixerControls.IParameter;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class ParameterViewModel : ReactiveObject, IParameter, IDisposable
{
    private readonly Node _node;

    public ParameterViewModel(float minimum, float maximum, string name,
        bool isMainParameter, string fullName, Node node)
    {
        Minimum = minimum;
        Maximum = maximum;
        Name = name;
        IsMainParameter = isMainParameter;

        FullyQualifiedName = fullName;
        _node = node;

        node.ParamsChanged += OnParameterChanged;
    }

    private void OnParameterChanged(
        Dictionary<string, Fr.Sonic.Model.Params.IParameter>?
            parameters)
    {
        if (!(parameters?.TryGetValue(FullyQualifiedName, out var value) ??
              false))
            return;

        if (value is Parameter<float> floatValue)
        {
            _internalChanges = true;
            Value = floatValue.Value;
            _internalChanges = false;
        }
    }

    private bool _internalChanges;

    public float Value
    {
        get;
        set
        {
            if (field.Equals(value))
                return;

            this.RaiseAndSetIfChanged(ref field, value);

            if (!_internalChanges)
                _node.SetParam(FullyQualifiedName, value);
        }
    }

    public float Minimum { get; }
    public float Maximum { get; }
    public string Name { get; }
    public string FullyQualifiedName { get; }

    public bool IsMainParameter { get; }

    public void Dispose()
    {
        _node.ParamsChanged -= OnParameterChanged;
        GC.SuppressFinalize(this);
    }
}
