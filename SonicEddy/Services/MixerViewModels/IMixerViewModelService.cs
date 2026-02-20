using ReactiveUI;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Services.MixerViewModels;

public interface IMixerViewModelService
{
    MixerViewModel ConvertMixerToViewModel(Mixer mixer,
        string? urlSegment, IScreen hostScreen);
}