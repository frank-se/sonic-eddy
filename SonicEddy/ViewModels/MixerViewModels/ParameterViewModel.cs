using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
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

    public ParameterViewModel(Node captureNode)
    {
        _captureNode = captureNode;

        this.WhenAnyValue(x => x.Value)
            .Subscribe(_ =>
            {
            })
            .DisposeWith(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}