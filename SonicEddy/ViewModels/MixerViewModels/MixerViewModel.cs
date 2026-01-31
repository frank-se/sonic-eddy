using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Config.LoopbackModule;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViews;

namespace SonicEddy.ViewModels.MixerViewModels;

public class MixerViewModel : ViewModelBase, IActivatableViewModel,
    IRoutableViewModel
{
    public ObservableCollection<ChannelStripViewModel> Channels { get; } = [];

    private IAppDataService _appDataService;
    private readonly IWireplumberService _wireplumberService;

    public MixerViewModel(IAppDataService appDataService,
        string? urlPathSegment, IScreen hostScreen,
        IWireplumberService wireplumberService)
    {
        _appDataService = appDataService;
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;
        _wireplumberService = wireplumberService;
    }

    public async Task AddChannel()
    {
        var currentMaxChannelId = Channels.Count == 0
            ? 0
            : Channels.Select(c => c.ChannelId).Max();

        var viewModel = new AddMixerChannelViewModel(_wireplumberService,
            Channels.Select(c => c.PlaybackNode.ObjectSerial).ToList());
        var dialog = new AddMixerChannelView()
        {
            DataContext = viewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (viewModel is { DialogResult: true, SelectedNode: not null })
        {
            var channelId = currentMaxChannelId + 1;

            var playbackNode = viewModel.SelectedNode.Node;
            var loopbackModule = await Fr.Wireplumber.Wireplumber.ModuleFactory
                .CreateLoopbackModuleAsync($"channel-{channelId}-loopback",
                    new()
                    {
                        CaptureProps = new()
                        {
                            Name = $"channel-{channelId}-loopback-capture",
                            Description = $"channel-{channelId}-loopback-capture",
                            AutoConnect = true,
                            TargetObject = playbackNode.ObjectSerial.ToString(),
                            MediaClass = "Stream/Input/Audio"
                        },
                        PlaybackProps = new()
                        {
                            Name = $"channel-{channelId}-loopback-playback",
                            Description = $"channel-{channelId}-loopback-playback",
                            AudioPosition = [ "FL", "FR" ],
                            AutoConnect = true,
                            MediaClass = "Stream/Output/Audio"
                        }
                    });

            Channels.Add(new(_appDataService, channelId,
                viewModel.SelectedNode.Node, loopbackModule)
            {
                ObjectSerial = viewModel.SelectedNode.Node.ObjectId
            });
        }
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();
}