using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using SonicEddy.Services.ClickSync;
using Splat;

namespace SonicEddy.ViewModels.ClickSyncViewModels;

public sealed class ClickSyncViewModel : ViewModelBase
{
    private readonly IClickSyncService? _service;
    private bool _refreshing;

    public ObservableCollection<ClickSyncConverterViewModel> Converters
    {
        get;
    } = [];

    public ObservableCollection<ClickSyncPortViewModel> ClickPorts { get; } =
        [];

    public ObservableCollection<ClickSyncPortViewModel> ResetPorts { get; } =
        [];

    public ObservableCollection<ClickSyncPortViewModel> RunPorts { get; } =
        [];

    public ClickSyncConverterViewModel? SelectedConverter
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RefreshPorts();
            this.RaisePropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedConverter is not null;

    public string NewName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Click sync";

    public decimal NewPulsesPerQuarterNote
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 24;

    public decimal NewPulseLengthMs
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 5.0m;

    public decimal NewPulseAmplitude
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 0.75m;

    public bool CanCreate
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Status
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ClickSyncViewModel()
        : this(Locator.Current.GetService<IClickSyncService>())
    {
    }

    public ClickSyncViewModel(IClickSyncService? service)
    {
        _service = service;
        if (_service is not null)
            _service.Changed += OnServiceChanged;
        Refresh();
    }

    public void Refresh()
    {
        _refreshing = true;
        try
        {
            var selectedId = SelectedConverter?.Id;
            Converters.Clear();
            if (_service is null)
            {
                CanCreate = false;
                Status = "Click sync service is unavailable.";
                SelectedConverter = null;
                return;
            }

            foreach (var converter in _service.GetConverters())
                Converters.Add(new(converter));

            SelectedConverter = Converters.FirstOrDefault(x =>
                                    x.Id == selectedId) ??
                                Converters.FirstOrDefault();
            CanCreate = _service.CanCreate;
            Status = CanCreate
                ? $"{Converters.Count} click sync converter(s)."
                : "Stop playback to create a converter.";
        }
        finally
        {
            _refreshing = false;
        }
    }

    public async Task Create()
    {
        if (_service is null || !CanCreate) return;
        try
        {
            await _service.AddConverterAsync(NewName,
                (uint)NewPulsesPerQuarterNote,
                (double)NewPulseLengthMs, (float)NewPulseAmplitude);
            Status = "Click sync converter created.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    public async Task Delete()
    {
        if (_service is null || SelectedConverter is null) return;
        await _service.DeleteConverterAsync(SelectedConverter.Id);
    }

    private void RefreshPorts()
    {
        ClickPorts.Clear();
        ResetPorts.Clear();
        RunPorts.Clear();
        if (_service is null || SelectedConverter is null) return;

        foreach (var state in _service.GetPorts(SelectedConverter.Id,
                     ClickSyncOutput.Click))
            ClickPorts.Add(new(state.Port, state.Selected, state.Linked,
                port => ApplyPort(ClickSyncOutput.Click, port)));
        foreach (var state in _service.GetPorts(SelectedConverter.Id,
                     ClickSyncOutput.Reset))
            ResetPorts.Add(new(state.Port, state.Selected, state.Linked,
                port => ApplyPort(ClickSyncOutput.Reset, port)));
        foreach (var state in _service.GetPorts(SelectedConverter.Id,
                     ClickSyncOutput.Run))
            RunPorts.Add(new(state.Port, state.Selected, state.Linked,
                port => ApplyPort(ClickSyncOutput.Run, port)));
    }

    private void ApplyPort(ClickSyncOutput output,
        ClickSyncPortViewModel port)
    {
        if (_refreshing || SelectedConverter is null) return;
        _service?.SetTarget(SelectedConverter.Id, output, port.Port,
            port.Selected);
    }

    private void OnServiceChanged() =>
        Dispatcher.UIThread.Post(Refresh);
}
