using System.Collections.ObjectModel;
using Fr.Lv2.Model;
using ReactiveUI;

namespace SonicEddy.ViewModels.CreateModuleDialogViewModels;

public class Lv2PluginNode : ReactiveObject
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public ObservableCollection<PluginDescription> AvailablePlugins
    {
        get;
        set;
    }

    private PluginDescription? _selectedPlugin = null;
    
    public PluginDescription? SelectedPlugin
    {
        get => _selectedPlugin;
        set => this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
    }
    
    public Lv2PluginNode(
        ObservableCollection<PluginDescription> availablePlugins)
    {
        AvailablePlugins = availablePlugins;
    }
}