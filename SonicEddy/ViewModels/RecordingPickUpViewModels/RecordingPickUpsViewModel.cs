using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Sonic.Model.Objects;
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

        PickUps.AddRange(_service.PickUps.Select(p => new RecordingPickUpViewModel(p, _service)));
    }

    public ObservableCollection<RecordingPickUpViewModel> PickUps { get; } = [];

    public async Task AddPickUp()
    {
        var sourceNodes = new ObservableCollection<Node>(_wireplumberService.GetPlaybackNodes());
        var destNodes = new ObservableCollection<Node>(_wireplumberService.GetCaptureNodes());

        var viewModel = new AddRecordingPickUpDialogViewModel(sourceNodes, destNodes);
        var dialog = new AddRecordingPickUpDialogView { DataContext = viewModel };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (viewModel is { DialogResult: true, IsValid: true })
        {
            await _service.AddPickUp(
                viewModel.Name,
                viewModel.SelectedSourceNode!,
                viewModel.SelectedDestNode!);
        }

        viewModel.Dispose();
    }

    private void OnPickUpAdded(RecordingPickUp pickUp) =>
        PickUps.Add(new RecordingPickUpViewModel(pickUp, _service));

    public void Dispose()
    {
        _service.Added -= OnPickUpAdded;
        GC.SuppressFinalize(this);
    }
}
