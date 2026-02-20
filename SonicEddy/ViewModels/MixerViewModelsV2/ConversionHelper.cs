using System.Collections.Generic;
using System.Linq;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Model.PropInfo;
using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public static class ConversionHelper
{
    public static List<ParameterCollection> GetCollectionFromFilterChainParams(
        Dictionary<string, Fr.Wireplumber.Model.Params.IParameter>? parameters,
        PropertyInfoCollection propertyInfoCollection, Node node)
    {
        if (parameters is null) return [];

        var result =
            new Dictionary<string,
                List<Fr.Wireplumber.Model.Params.IParameter>>();

        var candidates = parameters.Where(p => p.Key.Contains(':'));

        foreach (var parameter in candidates)
        {
            var key = parameter.Key.Split(':')[0];
            if (result.TryGetValue(key, out var value))
            {
                value.Add(parameter.Value);
            }
            else
            {
                result[key] = [parameter.Value];
            }
        }

        return result.Select(d =>
            {
                var parameterViewModels = d.Value.Select(p =>
                {
                    var info =
                        propertyInfoCollection.PropertyInfos.FirstOrDefault(i =>
                            i.Name == p.Name);
                    if (info is null) return null;

                    if (info.PropertyType is not FloatRange range) return null;

                    var name = p.Name.Split(':')[1];

                    return new ParameterViewModel(range.Minimum, range.Maximum,
                        name, true, p.Name, node);
                }).OfType<IParameter>();

                return new ParameterCollection(d.Key,
                    parameterViewModels.ToList());
            })
            .ToList();
    }
}