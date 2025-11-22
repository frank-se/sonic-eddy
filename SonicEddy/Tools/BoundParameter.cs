using System;
using Avalonia;
using Avalonia.Threading;
using SonicEddy.Models.Plugins;

namespace SonicEddy.Tools;

public class BoundParameter : AvaloniaObject, IDisposable
{
    private readonly FilterGraphParameter _parameter;
    private bool _updating;

    public static readonly StyledProperty<float> ValueProperty =
        AvaloniaProperty.Register<BoundParameter, float>(nameof(Value),
            coerce: (_, value) => Math.Clamp(value, 0.0f, 1.0f));

    public BoundParameter(FilterGraphParameter parameter)
    {
        _parameter = parameter;
        _parameter.ValueChanged += OnParameterChanged;
        SetValue(ValueProperty, _parameter.NormalizedValue);
    }

    static BoundParameter()
    {
        ValueProperty.Changed.AddClassHandler<BoundParameter>((boundParameter,
            changedEventArgs) =>
        {
            if (boundParameter._updating) return;
            if (changedEventArgs.NewValue is not float newValue) return;

            boundParameter._updating = true;
            try
            {
                boundParameter._parameter.NormalizedValue = newValue;
            }
            finally
            {
                boundParameter._updating = false;
            }
        });
    }

    private void OnParameterChanged(float normalized)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_updating) return;
            _updating = true;
            try
            {
                SetValue(ValueProperty, normalized);
            }
            finally
            {
                _updating = false;
            }
        });
    }

    public float Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public void Dispose()
    {
        _parameter.ValueChanged -= OnParameterChanged;
    }
}