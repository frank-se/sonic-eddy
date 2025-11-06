using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModel;

public class MixerViewModel : ViewModelBase, IDisposable, IActivatableViewModel
{
    private readonly CompositeDisposable _disposables = new();
    private readonly SourceList<Stream> _streams = new();

    private readonly ReadOnlyObservableCollection<Stream> _streamsBindable;

    public MixerViewModel()
    {
        TargetObjects =
        [
            new("alsa_card.pci-0000_0a_00.4", 1, "RME RayDAT_a5963e",
                "DEV3"),
            new("alsa_card.pci-0000_08_00.1", 2, "HDA ATI HDMI",
                "HD Pro Webcam C920"),
            new(
                "alsa_card.usb-AKG_C44-USB_Microphone_AKG_C44-USB_Microphone-00",
                3, "AKG C44-USB Microphone", "DEV2"),
            new("alsa_card.pci-0000_0a_00.4", 4, "HD Pro Webcam C920",
                "DEV1")
        ];

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
                .Subscribe(TargetObjectChanged)
                .DisposeWith(disposables);
        });
    }

    public ObservableCollection<TargetObject> TargetObjects { get; set; }
    public ReadOnlyObservableCollection<Stream> Streams => _streamsBindable;

    public ViewModelActivator Activator { get; } = new();

    public void Dispose()
    {
        _streams.Dispose();
        _disposables.Dispose();
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
}