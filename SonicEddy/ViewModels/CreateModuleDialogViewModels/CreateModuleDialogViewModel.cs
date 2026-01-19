using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.CreateModuleDialogViewModels;

public enum ModuleTypeEnum
{
    FilterChain,
    Loopback,
    None
}

public record ModuleType(string Name, ModuleTypeEnum Type);

public class CreateModuleDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly IAppDataService _appDataService;
    private readonly IWireplumberService _wireplumberService;

    public CreateModuleDialogViewModel(IAppDataService appDataService,
        IWireplumberService wireplumberService)
    {
        _appDataService = appDataService;
        _wireplumberService = wireplumberService;

        Task.Run(async () =>
        {
            var filterGraphs =
                (await _appDataService.GetAllFilterGraphs()).Select(f =>
                    new FilterGraphViewModel() { Id = f.Id, Name = f.Name });
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FilterGraphs.AddRange(filterGraphs);
            });
        });

        CaptureProps =
            new("Stream/Input/Audio",
                CaptureTargetObjects);

        PlaybackProps =
            new("Stream/Output/Audio",
                PlaybackTargetObjects);

        this.WhenAnyValue(x => x.CaptureProps.Description)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.CaptureProps.Name)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.PlaybackProps.Description)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.PlaybackProps.Name)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedFilterGraph)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        CaptureTargetObjects.AddRange(_wireplumberService
            .GetTargetObjectsForCaptureNode()
            .Select(n => new TargetObjectViewModel()
            {
                Name = n.Name ?? string.Empty,
                Description = n.Description ?? string.Empty,
                ObjectSerial = n.ObjectSerial
            }));

        PlaybackTargetObjects.AddRange(_wireplumberService
            .GetTargetObjectsForPlaybackNode()
            .Select(n => new TargetObjectViewModel()
            {
                Name = n.Name ?? string.Empty,
                Description = n.Description ?? string.Empty,
                ObjectSerial = n.ObjectSerial
            }));

        SelectedModuleType = SupportedModules.First();
    }

    private void ValidateForm()
    {
        IsButtonEnabled = Name != string.Empty && CaptureProps.IsValid &&
                          PlaybackProps.IsValid &&
                          (SelectedModuleType.Type ==
                           ModuleTypeEnum.FilterChain &&
                           SelectedFilterGraph != null ||
                           SelectedModuleType.Type ==
                           ModuleTypeEnum.Loopback);
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<TargetObjectViewModel>
        CaptureTargetObjects { get; } = [];

    public NodePropertiesViewModel CaptureProps { get; }

    public ObservableCollection<TargetObjectViewModel> PlaybackTargetObjects
    {
        get;
    } = [];

    public NodePropertiesViewModel PlaybackProps { get; }

    public ObservableCollection<ModuleType> SupportedModules { get; } =
    [
        new("Filter Chain", ModuleTypeEnum.FilterChain),
        new("Loopback", ModuleTypeEnum.Loopback)
    ];

    public ModuleType SelectedModuleType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool DialogResult;

    public ObservableCollection<FilterGraphViewModel> FilterGraphs { get; } =
        [];

    public FilterGraphViewModel? SelectedFilterGraph
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task CancelAction()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task AddModuleAction()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public void Dispose() => _disposables.Dispose();
}