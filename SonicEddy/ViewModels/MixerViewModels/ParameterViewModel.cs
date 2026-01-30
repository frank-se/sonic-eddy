using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Fr.Wireplumber.Model.Objects;
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

    public ParameterViewModel(Node captureNode, string fullName)
    {
        _captureNode = captureNode;

        this.WhenAnyValue(x => x.Value)
            .Skip(2)
            .Subscribe(value =>
            {
                if (fullName is not null)
                {
                    _captureNode.SetParam(fullName, value);
                }
            })
            .DisposeWith(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}