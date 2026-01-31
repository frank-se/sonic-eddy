using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.Params;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class ParameterViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public float Minimum { get; init; }
    public float Maximum { get; init; }
    public required string Name { get; init; }

    public float Value
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private Node _captureNode;

    private float _knownValue;
    private string _fullName;

    public ParameterViewModel(Node captureNode, string fullName)
    {
        _captureNode = captureNode;
        _fullName = fullName;

        _captureNode.ParamsChanged += OnParamsChangedEvent;

        this.WhenAnyValue(x => x.Value)
            .Skip(2)
            .Subscribe(value => { _captureNode.SetParam(fullName, value); })
            .DisposeWith(_disposables);
    }

    private void OnParamsChangedEvent(
        Dictionary<string, IParameter>? parameters)
    {
        if (parameters?.TryGetValue(_fullName, out var value) ?? false)
        {
            if (value is Parameter<float> parameter)
            {
                if (Math.Abs(_knownValue - parameter.Value) > 0.0002)
                {
                    _knownValue = parameter.Value;
                    if (Math.Abs(Value - parameter.Value) > 0.0002)
                    {
                        Value = parameter.Value;
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _captureNode.ParamsChanged -= OnParamsChangedEvent;
    }
}