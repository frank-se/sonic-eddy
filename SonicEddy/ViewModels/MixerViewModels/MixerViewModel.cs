using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DynamicData;
using DynamicData.Binding;
using Fr.Wireplumber;
using ReactiveUI;
using SonicEddy.Audio;
using SonicEddy.Services.AppData;
using SonicEddy.Views.MixerViews;

namespace SonicEddy.ViewModels.MixerViewModels;

public class MixerViewModel : ViewModelBase, IDisposable, IActivatableViewModel
{
    private readonly IAppDataService _appDataService;
    private readonly CompositeDisposable _disposables = new();
    private readonly SourceList<Stream> _streams = new();

    private readonly ReadOnlyObservableCollection<Stream> _streamsBindable;
    private readonly Subject<Unit> _wasShutDown = new();

    public MixerViewModel(IAppDataService appDataService)
    {
        _appDataService = appDataService;
        TargetObjects = new()
        {
            Wireplumber.Nodes.Where(n =>
                    n.Media.Class is "Audio/Sink" or "Stream/Input/Audio")
                .Select(n => new TargetObject(n.Name ?? string.Empty,
                    n.ObjectSerial, n.Media.Class ?? string.Empty,
                    n.Description ?? string.Empty))
        };

        _streams.Connect()
            .Bind(out _streamsBindable)
            .Subscribe()
            .DisposeWith(_disposables);

        this.WhenActivated(disposables =>
        {
            _streams.Connect()
                .AutoRefresh(stream => stream.TargetObject)
                .WhenPropertyChanged<Stream, TargetObject?>(stream =>
                    stream.TargetObject, false)
                .TakeUntil(_wasShutDown)
                .Subscribe(TargetObjectChanged)
                .DisposeWith(disposables);

            _streams.Connect()
                .AutoRefresh(stream => stream.Volume)
                .WhenPropertyChanged(stream => stream.Volume, false)
                .Sample(TimeSpan.FromMilliseconds(100),
                    RxApp.MainThreadScheduler)
                .TakeUntil(_wasShutDown)
                .Subscribe(VolumeChanged)
                .DisposeWith(disposables);

            _streams.Connect()
                .AutoRefresh(stream => stream.Pan)
                .WhenPropertyChanged(stream => stream.Pan, false)
                .Sample(TimeSpan.FromMilliseconds(100),
                    RxApp.MainThreadScheduler)
                .TakeUntil(_wasShutDown)
                .Subscribe(PanChanged)
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

    public async Task AddStream()
    {
        var dialogViewModel = new AddStreamDialogViewModel();
        var dialog = new AddStreamDialog
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(GetMainWindow()!);

        if (dialogViewModel.DialogResult &&
            dialogViewModel.SelectedStream != null)
            _streams.Add(dialogViewModel.SelectedStream);
    }

    private Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private void PanChanged(PropertyValue<Stream, double> changedEvent)
    {
        SetChannelVolumeFromStream(changedEvent);
    }

    private void VolumeChanged(PropertyValue<Stream, double> changedEvent)
    {
        SetChannelVolumeFromStream(changedEvent);
    }

    private static void SetChannelVolumeFromStream(
        PropertyValue<Stream, double> changedEvent)
    {
        var volume = changedEvent.Value;
        var pan = changedEvent.Sender.Pan;
        var gains = Pan.GetGainsFromPanAndVolume(pan, volume);
        var objectId = changedEvent.Sender.ObjectId;
        Pipewire.SetChannelVolumeProps(objectId, [gains.Item1, gains.Item2]);
    }

    public void RemoveStream(Stream stream)
    {
        _streams.Remove(stream);
    }
    
    private void TargetObjectChanged(
        PropertyValue<Stream, TargetObject?> changedEvent)
    {
        var targetObject = changedEvent.Value;

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