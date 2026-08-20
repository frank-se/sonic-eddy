using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using SonicEddy.Services.CameraRouter;

namespace SonicEddy.ViewModels.CameraRouterViewModels;

public sealed class CameraSlotViewModel : ViewModelBase
{
    private const string NoneOption = "(None)";

    private readonly ICameraRouterService _cameraRouterService;
    private string? _selectedSource;
    private bool _connected;
    private bool _suppressAssign;

    public CameraSlotViewModel(CameraSlot slot,
        ICameraRouterService cameraRouterService,
        IReadOnlyList<string> candidateSources)
    {
        _cameraRouterService = cameraRouterService;
        Index = slot.Index;
        CandidateSources = [];

        _suppressAssign = true;
        Update(slot, candidateSources);
        _suppressAssign = false;
    }

    public int Index { get; }

    public string DisplayName => $"Camera {Index + 1}";

    public ObservableCollection<string> CandidateSources { get; }

    public string? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (_selectedSource == value) return;
            this.RaiseAndSetIfChanged(ref _selectedSource, value);
            if (_suppressAssign) return;
            _ = _cameraRouterService.AssignSlotAsync(Index,
                string.IsNullOrEmpty(value) || value == NoneOption
                    ? null
                    : value);
        }
    }

    public bool Connected
    {
        get => _connected;
        private set => this.RaiseAndSetIfChanged(ref _connected, value);
    }

    public string StatusText => Connected ? "Connected" : "Waiting ...";

    internal void Update(CameraSlot slot, IReadOnlyList<string> candidateSources)
    {
        var wasSuppressing = _suppressAssign;
        _suppressAssign = true;
        try
        {
            CandidateSources.Clear();
            CandidateSources.Add(NoneOption);
            foreach (var name in candidateSources)
                CandidateSources.Add(name);

            // Keep showing an assigned-but-not-currently-visible source so
            // the "Waiting ..." status has something to point at.
            if (!string.IsNullOrEmpty(slot.SourceNodeName) &&
                !CandidateSources.Contains(slot.SourceNodeName))
                CandidateSources.Add(slot.SourceNodeName);

            SelectedSource = string.IsNullOrEmpty(slot.SourceNodeName)
                ? NoneOption
                : slot.SourceNodeName;
        }
        finally
        {
            _suppressAssign = wasSuppressing;
        }

        Connected = slot.Connected;
        this.RaisePropertyChanged(nameof(StatusText));
    }
}
