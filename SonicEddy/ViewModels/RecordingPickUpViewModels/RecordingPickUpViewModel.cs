using System;
using ReactiveUI;
using SonicEddy.Services.RecordingPickUp;

namespace SonicEddy.ViewModels.RecordingPickUpViewModels;

public class RecordingPickUpViewModel : ViewModelBase
{
    private readonly IRecordingPickUpService _service;

    public RecordingPickUpViewModel(RecordingPickUp pickUp, IRecordingPickUpService service)
    {
        _service = service;
        PickUp = pickUp;
        Id = pickUp.Id;
        Name = pickUp.Name;
        _isActive = pickUp.IsActive;
        _trim = pickUp.Trim;
        SourceLabel = pickUp.SourceLabel;
        DestLabel = pickUp.DestNode.Description ?? pickUp.DestNode.Name ?? "—";
    }

    public RecordingPickUp PickUp { get; }
    public Guid Id { get; }
    public string Name { get; }
    public string SourceLabel { get; }
    public string DestLabel { get; }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            this.RaiseAndSetIfChanged(ref _isActive, value);
            _service.SetActive(Id, value);
        }
    }

    private double _trim;
    public double Trim
    {
        get => _trim;
        set
        {
            this.RaiseAndSetIfChanged(ref _trim, value);
            _service.SetTrim(Id, value);
        }
    }
}
