using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerData;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels.CustomControlTesterViewModels;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.ViewModels.FilterGraphManagerViewModels;
using SonicEddy.ViewModels.GraphControlTesterViewModels;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.ViewModels.MidiConnectionEditorViewModels;
using SonicEddy.ViewModels.MixerManagerViewModels;
using SonicEddy.ViewModels.MixerViewModels;
using SonicEddy.ViewModels.ModuleManagerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.ProAudioStreamsViewModels;
using Splat;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen
{
    public RoutingState Router { get; } = new();

    public MainWindowViewModel()
    {
        NavigateToMixerAction();
    }

    public bool ProAudioStreamsMenuItemSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MixerMenuItemSelected
    {
        get;
        set => field =
            this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ObjectBrowserMenuItemSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MetadataMenuItemSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ModuleManagerViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool FilterGraphBuilderViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool FilterGraphManagerViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CustomControlTesterViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MixerManagerViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool GraphControlTesterViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MidiConnectionEditorViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void NavigateToMixerAction()
    {
        MixerMenuItemSelected = true;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        var mixerService = Locator.Current.GetService<IMixerService>();
        Router.Navigate.Execute(new MixerViewModel(appDataService!, "mixer",
            this, new WireplumberService(), mixerService!));
    }

    public void NavigateToObjectBrowserAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = true;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(
            new ObjectBrowserViewModel(appDataService!, "object-browser",
                this));
    }

    public void NavigateToProAudioStreamsAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = true;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new ProAudioStreamsViewModel(appDataService!,
            "pro-audio-streams", this));
    }

    public void NavigateToMetadataView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = true;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new MetadataViewModel(appDataService!,
            "metadata", this));
    }

    public void NavigateToModuleManagerView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = true;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new ModuleManagerViewModel(appDataService!,
            "module-manager", this));
    }

    public void NavigateToFilterGraphBuilderView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = true;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new FilterGraphBuilderViewModel(appDataService!,
            "filter-graph-builder", this,
            Task.Run(Fr.Lv2.Lv2.ClassDescriptions),
            Task.Run(Fr.Lv2.Lv2.PluginDescriptions)));
    }

    public void NavigateToFilterGraphManagerView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = true;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new FilterGraphManagerViewModel(appDataService!,
            "filter-graph-builder", this));
    }

    public void NavigateToCustomControlTesterView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = true;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        Router.Navigate.Execute(
            new CustomControlTesterViewModel("control-tester", this));
    }

    public void NavigateToMixerManagerAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = true;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        var mixerService = Locator.Current.GetService<IMixerService>();
        Router.Navigate.Execute(new MixerManagerViewModel("mixer-manager",
            this, mixerService!));
    }

    public void NavigateToGraphControlTesterView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = true;
        MidiConnectionEditorViewSelected = false;
        Router.Navigate.Execute(
            new GraphControlTesterViewModel("graph-tester", this));
    }

    public void NavigateToMidiConnectionEditorView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = true;
        var wireplumberService =
            Locator.Current.GetService<IWireplumberService>();
        var ports = wireplumberService.GetMidiPorts();
        Router.Navigate.Execute(
            new MidiConnectionEditorViewModel(ports, "graph-tester", this));
    }
}