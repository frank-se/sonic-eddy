using System;
using System.Collections.Generic;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class FilterChainPresetViewModel
{
    public FilterChainPresetViewModel(FilterChainPreset preset)
    {
        Id = preset.Id;
        Name = preset.Name;
        Values = preset.Values;
    }

    public Guid Id { get; }
    public string Name { get; }
    public List<FilterChainPresetValue> Values { get; }
}
