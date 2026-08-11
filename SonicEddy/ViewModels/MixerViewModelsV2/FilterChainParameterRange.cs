using Fr.Sonic.Model.PropInfo;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

internal static class FilterChainParameterRange
{
    public static (float Min, float Max) Get(PropertyInfo? info) =>
        info?.PropertyType switch
        {
            FloatRange p => (p.Minimum, p.Maximum),
            FloatStepRange p => (p.Minimum, p.Maximum),
            DoubleRange p => ((float)p.Minimum, (float)p.Maximum),
            DoubleStepRange p => ((float)p.Minimum, (float)p.Maximum),
            IntegerRange p => (p.Minimum, p.Maximum),
            IntegerStepRange p => (p.Minimum, p.Maximum),
            LongRange p => (p.Minimum, p.Maximum),
            LongStepRange p => (p.Minimum, p.Maximum),
            BoolEnum => (0f, 1f),
            _ => (0f, 1f)
        };
}
