using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Fr.Wireplumber;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModel;

public class MixerViewModel : ViewModelBase, IDisposable, IActivatableViewModel
{
    private readonly CompositeDisposable _disposables = new();
    private readonly SourceList<Stream> _streams = new();

    private readonly ReadOnlyObservableCollection<Stream> _streamsBindable;
    private readonly Subject<Unit> _wasShutDown = new();

    public MixerViewModel()
    {
        TargetObjects = new()
        {
            Wireplumber.Nodes.Where(n =>
                    n.Media.Class is "Audio/Sink" or "Stream/Input/Audio")
                .Select(n => new TargetObject(n.Name ?? string.Empty,
                    n.ObjectSerial, n.Media.Class ?? string.Empty,
                    n.Description ?? string.Empty))
        };

        var initialData = new List<Stream>
        {
            new("Firefox", 123, 2.0, TargetObjects.First()),
            new("", 123, 2.0, TargetObjects.Skip(2)
                .First()),
            new("", 123, 2.0, null)
        };

        _streams.AddRange(initialData);

        _streams.Connect()
            .Bind(out _streamsBindable)
            .Subscribe()
            .DisposeWith(_disposables);

        this.WhenActivated(disposables =>
        {
            _streams.Connect()
                .AutoRefresh(stream => stream.TargetObject)
                .WhenValueChanged<Stream, TargetObject?>(stream =>
                    stream.TargetObject, false)
                .TakeUntil(_wasShutDown)
                .Subscribe(TargetObjectChanged)
                .DisposeWith(disposables);
        });
    }

    public ObservableCollection<TargetObject> TargetObjects { get; set; }
    public ReadOnlyObservableCollection<Stream> Streams => _streamsBindable;
    public ViewModelActivator Activator { get; } = new();

    public void Dispose()
    {
        _disposables.Dispose();
        _streams.Dispose();
        _wasShutDown.Dispose();
        GC.SuppressFinalize(this);
    }

    private void TargetObjectChanged(TargetObject? targetObject)
    {
        if (targetObject is null)
            Console.WriteLine("Target Object changed to null");
        if (targetObject is not null)
            Console.WriteLine(
                $"Target Object changed to {targetObject.Description}");
    }

    public void StopProcessing()
    {
        _wasShutDown.OnNext(Unit.Default);
        _wasShutDown.OnCompleted();
    }
}