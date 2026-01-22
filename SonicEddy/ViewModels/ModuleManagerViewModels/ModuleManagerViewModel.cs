using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using DynamicData;
using Fr.Wireplumber.Modules.Models;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.ViewModels.CreateModuleDialogViewModels;
using SonicEddy.Views.CreateModuleDialogViews;

namespace SonicEddy.ViewModels.ModuleManagerViewModels;

public class ModuleManagerViewModel : ViewModelBase, IRoutableViewModel,
    IActivatableViewModel, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    public ObservableCollection<PipewireModule> Modules { get; } = [];

    public PipewireModule? SelectedModule
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ModuleViewModelBase? SelectedModuleViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ModuleManagerViewModel(
        IAppDataService appDataService,
        string? urlPathSegment,
        IScreen hostScreen)
    {
        HostScreen = hostScreen;
        UrlPathSegment = urlPathSegment;
        _appDataService = appDataService;

        _ = Task.Run(LoadModules);

        this.WhenAnyValue(x => x.SelectedModule)
            .Subscribe(_ => UpdateSelectedModuleViewModel())
            .DisposeWith(_disposables);
    }

    private void UpdateSelectedModuleViewModel()
    {
        SelectedModuleViewModel = SelectedModule switch
        {
            FilterChain f => new FilterChainModuleDetailsViewModel()
            {
                Name = f.Name,
                CaptureNode = new()
                {
                    Description = f.CaptureNode.Description!,
                    Name = f.CaptureNode.Name!,
                    ObjectSerial = f.CaptureNodeObjectSerial,
                    PropertyInfos = f.CaptureNode.PropertyInfos.IsCompleted
                        ? f.CaptureNode.PropertyInfos.Result.PropertyInfos
                            .Select(PropertyInfoViewModel.FromPropertyInfo)
                            .ToList()
                        : []
                },
                PlaybackNode = new()
                {
                    Name = f.PlaybackNode.Name!,
                    Description = f.PlaybackNode.Description!,
                    ObjectSerial = f.PlaybackNodeObjectSerial,
                    PropertyInfos = f.PlaybackNode.PropertyInfos.IsCompleted
                        ? f.PlaybackNode.PropertyInfos.Result.PropertyInfos
                            .Select(PropertyInfoViewModel.FromPropertyInfo)
                            .ToList()
                        : []
                }
            },
            null => null,
            _ => throw new NotImplementedException()
        };
    }

    public void SetSelectedModule(PipewireModule module)
    {
        SelectedModule = module;
    }

    private void LoadModules()
    {
        var modules = Fr.Wireplumber.Wireplumber.ModuleRegistry.Modules;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Modules.Clear();
            Modules.AddRange(modules);
        });
    }

    private readonly IAppDataService _appDataService;
    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();

    public async Task CreateModule()
    {
        var dialogViewModel =
            new CreateModuleDialogViewModel(_appDataService,
                new WireplumberService());
        var dialog = new CreateModuleDialogView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel.DialogResult)
        {
            switch (dialogViewModel.SelectedModuleType.Type)
            {
                case ModuleTypeEnum.FilterChain:
                {
                    var filterGraph =
                        await _appDataService.GetFilterGraph(
                            dialogViewModel.SelectedFilterGraph!.Id);
                    var config =
                        dialogViewModel.ToFilterChainConfig(filterGraph);
                    await Fr.Wireplumber.Wireplumber.ModuleFactory
                        .CreateFilterChainAsync(dialogViewModel.Name, config);
                    _ = Task.Run(LoadModules);
                }
                    break;
                case ModuleTypeEnum.Loopback:
                    break;
                case ModuleTypeEnum.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        Activator.Dispose();
    }
}