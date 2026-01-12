using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.ViewModels.CreateModuleDialogViewModels;
using SonicEddy.Views.CreateModuleDialogView;

namespace SonicEddy.ViewModels.ModuleViewModels;

public class ModuleManagerViewModel : ViewModelBase, IRoutableViewModel,
    IActivatableViewModel
{
    public ObservableCollection<PipewireModule> Modules { get; } = [];

    public ModuleManagerViewModel(
        IAppDataService appDataService,
        string? urlPathSegment,
        IScreen hostScreen)
    {
        HostScreen = hostScreen;
        UrlPathSegment = urlPathSegment;
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();

    public async Task CreateModule()
    {
        var dialogViewModel = new CreateModuleDialogViewModel();
        var dialog = new CreateModuleDialogView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel.DialogResult)
        {
        }
    }
}