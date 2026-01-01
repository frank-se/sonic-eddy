using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Fr.Wireplumber;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.Views.ProAudioStreamsViews;

namespace SonicEddy.ViewModels.ProAudioStreamsViewModels;

public class ProAudioStreamsViewModel : ViewModelBase
{
    private readonly IAppDataService _appDataService;

    public ProAudioStreamsViewModel(IAppDataService appDataService)
    {
        _appDataService = appDataService;
    }

    public ObservableCollection<ProAudioStreamLoopback>
        ProAudioStreams { get; } = [];

    public async Task AddStreamLoopback()
    {
        var dialogViewModel = new AddProAudioStreamDialogViewModel();
        var dialog = new AddProAudioStreamDialogView
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel is
            { DialogResult: true, SelectedDeviceNode: not null })
        {
            var selectedSourceNode = dialogViewModel.SelectedDeviceNode;
            ProAudioStreams.Add(new(
                new(selectedSourceNode.ObjectSerial,
                    selectedSourceNode.ObjectId, selectedSourceNode.Name!,
                    selectedSourceNode.Description!),
                dialogViewModel.Name,
                dialogViewModel.Description,
                dialogViewModel.LeftPortId,
                dialogViewModel.RightPortId
            ));

            throw new NotImplementedException(
                "Creating a loopback module is currently broken");
        }
    }
}