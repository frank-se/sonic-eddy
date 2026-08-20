using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using SonicEddy.Services.CameraRouter;
using Splat;

namespace SonicEddy.ViewModels.CameraRouterViewModels;

public sealed class CameraRouterViewModel : ViewModelBase, IDisposable
{
    private readonly ICameraRouterService _cameraRouterService;

    public CameraRouterViewModel()
    {
        _cameraRouterService =
            Locator.Current.GetService<ICameraRouterService>() ??
            throw new InvalidOperationException(
                "Camera router service is not registered.");
        _cameraRouterService.SlotsChanged += OnSlotsChanged;

        var candidateNames = GetCandidateNames();
        Slots = new ObservableCollection<CameraSlotViewModel>(
            _cameraRouterService.Slots.Select(slot =>
                new CameraSlotViewModel(slot, _cameraRouterService,
                    candidateNames)));
    }

    public ObservableCollection<CameraSlotViewModel> Slots { get; }

    private void OnSlotsChanged() => Dispatcher.UIThread.Post(RefreshSlots);

    private void RefreshSlots()
    {
        var candidateNames = GetCandidateNames();
        foreach (var slot in _cameraRouterService.Slots)
        {
            var slotViewModel =
                Slots.FirstOrDefault(s => s.Index == slot.Index);
            slotViewModel?.Update(slot, candidateNames);
        }
    }

    private string[] GetCandidateNames() =>
        _cameraRouterService.GetCandidateSources()
            .Select(node => node.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToArray();

    public void Dispose()
    {
        _cameraRouterService.SlotsChanged -= OnSlotsChanged;
        GC.SuppressFinalize(this);
    }
}
