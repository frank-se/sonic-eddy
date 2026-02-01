using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Wireplumber.Model.Config.LoopbackModule;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerData;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViews;

namespace SonicEddy.ViewModels.MixerViewModels;

public class MixerViewModel : ViewModelBase, IActivatableViewModel,
    IRoutableViewModel
{
    public ObservableCollection<ChannelStripViewModel> Channels { get; } = [];

    private readonly IAppDataService _appDataService;
    private readonly IWireplumberService _wireplumberService;
    private readonly IMixerService _mixerService;

    public MixerViewModel(IAppDataService appDataService,
        string? urlPathSegment, IScreen hostScreen,
        IWireplumberService wireplumberService,
        IMixerService mixerService)
    {
        _appDataService = appDataService;
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;
        _wireplumberService = wireplumberService;
        _mixerService = mixerService;

        UpdateChannelStripFromMixer(_mixerService.CurrentMixer);
    }

    private ChannelStripViewModel ToChannelStripViewModel(
        ChannelStrip channelStrip) => new(_appDataService,
        channelStrip.ChannelId, channelStrip.InputNode,
        channelStrip.FilterModule,
        channelStrip.LoopbackModule, _mixerService)
    {
    };

    public async Task SaveMixer()
    {
        await _mixerService.PersistCurrentMixer();
    }
    
    public async Task AddChannel()
    {
        var viewModel = new AddMixerChannelViewModel(_wireplumberService,
            Channels.Select(c => c.PlaybackNode.ObjectSerial).ToList());
        var dialog = new AddMixerChannelView()
        {
            DataContext = viewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (viewModel is { DialogResult: true, SelectedNode: not null })
        {
            var mixer =
                await _mixerService.AddChannelStripToCurrentMixer(
                    viewModel.Name,
                    viewModel.SelectedNode.Node);

            UpdateChannelStripFromMixer(mixer);
        }
    }

    private void UpdateChannelStripFromMixer(Mixer mixer)
    {
        if (Channels.Count == mixer.ChannelStrips.Count) return;

        if (Channels.Count == mixer.ChannelStrips.Count + 1)
        {
            // we're adding one, so just add the last
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Channels.Add(
                    ToChannelStripViewModel(mixer.ChannelStrips.Last()));
            });
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Channels.Clear();
            Channels.AddRange(
                mixer.ChannelStrips.Select(ToChannelStripViewModel));
        });
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();
}