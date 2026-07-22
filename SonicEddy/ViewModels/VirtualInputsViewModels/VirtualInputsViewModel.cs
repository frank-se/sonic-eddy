using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.Views.VirtualInputsViews;

namespace SonicEddy.ViewModels.VirtualInputsViewModels;

public class VirtualInputsViewModel : ViewModelBase, IDisposable
{
    private readonly IWireplumberService _wireplumberService;
    private readonly IVirtualInputService _virtualInputService;

    public VirtualInputsViewModel(
        IWireplumberService wireplumberService,
        IVirtualInputService virtualInputService)
    {
        _wireplumberService = wireplumberService;

        _virtualInputService = virtualInputService;
        _virtualInputService.Added += OnVirtualInputAdded;

        VirtualInputs.AddRange(
            _virtualInputService.VirtualInputs.Select(i =>
                new VirtualInputViewModel(i)));
    }

    public ObservableCollection<VirtualInputViewModel> VirtualInputs { get; } =
        [];

    private void OnVirtualInputAdded(VirtualInput input)
    {
        VirtualInputs.Add(new(input));
    }

    public async Task DeleteVirtualInput(object viewModel)
    {
        var virtualInputViewModel = (VirtualInputViewModel)viewModel;
        await _virtualInputService.DeleteVirtualInput(virtualInputViewModel.VirtualInput);
        VirtualInputs.Remove(virtualInputViewModel);
    }

    public async Task AddVirtualInput()
    {
        var viewModel =
            new AddVirtualInputDialogViewModel(
                new(_wireplumberService.GetPlaybackNodes()),
                _wireplumberService);

        var dialog = new AddVirtualInputDialogView()
        {
            DataContext = viewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (viewModel is { DialogResult: true, IsValid: true })
        {
            var ports = viewModel.IsMono
                ? [viewModel.SelectedLeftPort!]
                : new[] { viewModel.SelectedLeftPort!, viewModel.SelectedRightPort! };
            await _virtualInputService.AddVirtualInput(
                viewModel.Name,
                viewModel.SelectedNode!,
                ports);
        }
    }

    public void Dispose()
    {
        _virtualInputService.Added -= OnVirtualInputAdded;

        GC.SuppressFinalize(this);
    }
}