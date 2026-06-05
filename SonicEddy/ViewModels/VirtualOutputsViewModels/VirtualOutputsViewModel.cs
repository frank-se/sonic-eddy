using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using SonicEddy.Services.VirtualOutputs;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.Views.VirtualOutputsViews;

namespace SonicEddy.ViewModels.VirtualOutputsViewModels;

public sealed class VirtualOutputsViewModel : ViewModelBase, IDisposable
{
    private readonly IWireplumberService _wireplumberService;
    private readonly IVirtualOutputService _virtualOutputService;

    public VirtualOutputsViewModel(
        IWireplumberService wireplumberService,
        IVirtualOutputService virtualOutputService)
    {
        _wireplumberService = wireplumberService;
        _virtualOutputService = virtualOutputService;
        _virtualOutputService.Added += OnVirtualOutputAdded;

        VirtualOutputs.AddRange(
            _virtualOutputService.VirtualOutputs.Select(output =>
                new VirtualOutputViewModel(output)));
    }

    public ObservableCollection<VirtualOutputViewModel> VirtualOutputs { get; }
        = [];

    public async Task AddVirtualOutput()
    {
        var viewModel = new AddVirtualOutputDialogViewModel(
            new(_wireplumberService.GetCaptureNodes()),
            _wireplumberService);

        var dialog = new AddVirtualOutputDialogView
        {
            DataContext = viewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (viewModel is { DialogResult: true, IsValid: true })
        {
            await _virtualOutputService.AddVirtualOutput(
                viewModel.Name,
                viewModel.SelectedNode!,
                [viewModel.SelectedLeftPort!, viewModel.SelectedRightPort!]);
        }
    }

    private void OnVirtualOutputAdded(VirtualOutput output) =>
        VirtualOutputs.Add(new(output));

    public void Dispose()
    {
        _virtualOutputService.Added -= OnVirtualOutputAdded;
        GC.SuppressFinalize(this);
    }
}
