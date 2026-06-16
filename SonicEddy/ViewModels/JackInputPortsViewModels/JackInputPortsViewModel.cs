using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using SonicEddy.Services.JackInputPorts;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.Views.JackInputPortsViews;

namespace SonicEddy.ViewModels.JackInputPortsViewModels;

public class JackInputPortsViewModel : ViewModelBase, IDisposable
{
    private readonly IJackInputPortService _jackInputPortService;
    private readonly IWireplumberService _wireplumberService;

    public JackInputPortsViewModel(
        IJackInputPortService jackInputPortService,
        IWireplumberService wireplumberService)
    {
        _jackInputPortService = jackInputPortService;
        _wireplumberService = wireplumberService;

        _jackInputPortService.Added += OnPortAdded;
        _jackInputPortService.Deleted += OnPortDeleted;

        JackInputPorts.AddRange(
            _jackInputPortService.JackInputPorts.Select(p =>
                new JackInputPortViewModel(p, wireplumberService)));
    }

    public ObservableCollection<JackInputPortViewModel> JackInputPorts { get; } = [];

    private void OnPortAdded(JackInputPort port) =>
        JackInputPorts.Add(new(port, _wireplumberService));

    private void OnPortDeleted(JackInputPort port)
    {
        var vm = JackInputPorts.FirstOrDefault(v =>
            ReferenceEquals(v.Port, port));
        if (vm is null) return;
        vm.Dispose();
        JackInputPorts.Remove(vm);
    }

    public async Task AddJackInputPort()
    {
        var dialogVm = new AddJackInputPortDialogViewModel(_wireplumberService);
        var dialog = new AddJackInputPortDialogView { DataContext = dialogVm };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (!dialogVm.DialogResult || !dialogVm.IsValid) return;

        var portNames = dialogVm.IsMono
            ? [dialogVm.SelectedLeftPort!.Name ?? string.Empty]
            : new[]
            {
                dialogVm.SelectedLeftPort!.Name ?? string.Empty,
                dialogVm.SelectedRightPort!.Name ?? string.Empty
            };

        await _jackInputPortService.AddJackInputPort(
            dialogVm.Name,
            dialogVm.SelectedNode!.Name ?? string.Empty,
            portNames);
    }

    public async Task DeleteJackInputPort(JackInputPortViewModel viewModel)
    {
        await _jackInputPortService.DeleteJackInputPort(viewModel.Port);
    }

    public void Dispose()
    {
        _jackInputPortService.Added -= OnPortAdded;
        _jackInputPortService.Deleted -= OnPortDeleted;

        foreach (var vm in JackInputPorts)
            vm.Dispose();

        GC.SuppressFinalize(this);
    }
}
