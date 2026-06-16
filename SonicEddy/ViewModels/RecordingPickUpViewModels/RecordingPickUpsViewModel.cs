using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using SonicEddy.Services.RecordingPickUp;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.Views.RecordingPickUpViews;

namespace SonicEddy.ViewModels.RecordingPickUpViewModels;

public class RecordingPickUpsViewModel : ViewModelBase, IDisposable
{
    private readonly IRecordingPickUpService _service;
    private readonly IWireplumberService _wireplumberService;

    public RecordingPickUpsViewModel(
        IRecordingPickUpService service,
        IWireplumberService wireplumberService)
    {
        _service = service;
        _wireplumberService = wireplumberService;
        _service.Added += OnPickUpAdded;
        _service.Deleted += OnPickUpDeleted;

        PickUps.AddRange(_service.PickUps.Select(p => new RecordingPickUpViewModel(p, _service)));
    }

    public ObservableCollection<RecordingPickUpViewModel> PickUps { get; } = [];

    public async Task AddPickUp()
    {
        var destNodes = new ObservableCollection<Fr.Sonic.Model.Objects.Node>(
            _wireplumberService.GetCaptureNodes());

        var viewModel = new AddRecordingPickUpDialogViewModel(destNodes);
        var dialog = new AddRecordingPickUpDialogView { DataContext = viewModel };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (viewModel is { DialogResult: true, IsValid: true })
        {
            await _service.AddPickUp(
                viewModel.Name,
                viewModel.BuildSourceKey(),
                viewModel.SelectedPosition!.Value,
                viewModel.SelectedDestNode!);
        }

        viewModel.Dispose();
    }

    public async Task DeletePickUp(RecordingPickUpViewModel viewModel)
    {
        await _service.DeletePickUp(viewModel.PickUp);
    }

    private void OnPickUpAdded(RecordingPickUp pickUp) =>
        PickUps.Add(new RecordingPickUpViewModel(pickUp, _service));

    private void OnPickUpDeleted(RecordingPickUp pickUp)
    {
        var vm = PickUps.FirstOrDefault(v => ReferenceEquals(v.PickUp, pickUp));
        if (vm is not null)
            PickUps.Remove(vm);
    }

    public void Dispose()
    {
        _service.Added -= OnPickUpAdded;
        _service.Deleted -= OnPickUpDeleted;
        GC.SuppressFinalize(this);
    }
}
