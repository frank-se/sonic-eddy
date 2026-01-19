using System;
using System.Globalization;
using Fr.Wireplumber.Model.PropInfo;

namespace SonicEddy.ViewModels.ModuleManagerViewModels;

public enum PropertyInfoType
{
    BoolEnum,
    DoubleRange,
    DoubleStepRange,
    FloatRange,
    FloatStepRange,
    IntegerRange,
    IntegerStepRange,
    LongRange,
    LongStepRange,
    StringValues,
    None
}

public class PropertyInfoViewModel
{
    public required string Name { get; init; }

    public required PropertyInfoType Type { get; init; }

    public required string Default { get; init; }

    public required string Minimum { get; init; }

    public required string Maximum { get; init; }

    public required string Step { get; init; }

    public required bool IsParam { get; init; }

    public static PropertyInfoViewModel FromPropertyInfo(
        PropertyInfo propertyInfo)
    {
        var type = propertyInfo.PropertyType switch
        {
            BoolEnum => PropertyInfoType.BoolEnum,
            DoubleRange => PropertyInfoType.DoubleRange,
            DoubleStepRange => PropertyInfoType.DoubleStepRange,
            FloatRange => PropertyInfoType.FloatRange,
            FloatStepRange => PropertyInfoType.FloatStepRange,
            IntegerRange => PropertyInfoType.IntegerRange,
            IntegerStepRange => PropertyInfoType.IntegerStepRange,
            LongRange => PropertyInfoType.LongRange,
            LongStepRange => PropertyInfoType.LongStepRange,
            StringValues => PropertyInfoType.StringValues,
            null => PropertyInfoType.None,
            _ => throw new NotImplementedException()
        };

        var @default = propertyInfo.PropertyType switch
        {
            BoolEnum p => p.Default.ToString(),
            DoubleRange p => p.Default.ToString(CultureInfo.CurrentCulture),
            DoubleStepRange p => p.Default.ToString(CultureInfo.CurrentCulture),
            FloatRange p => p.Default.ToString(CultureInfo.CurrentCulture),
            FloatStepRange p => p.Default.ToString(CultureInfo.CurrentCulture),
            IntegerRange p => p.Default.ToString(),
            IntegerStepRange p => p.Default.ToString(),
            LongRange p => p.Default.ToString(),
            LongStepRange p => p.Default.ToString(),
            StringValues p => p.Value,
            null => string.Empty,
            _ => throw new NotImplementedException()
        };

        var minimum = propertyInfo.PropertyType switch
        {
            BoolEnum p => string.Empty,
            DoubleRange p => p.Minimum.ToString(CultureInfo.CurrentCulture),
            DoubleStepRange p => p.Minimum.ToString(CultureInfo.CurrentCulture),
            FloatRange p => p.Minimum.ToString(CultureInfo.CurrentCulture),
            FloatStepRange p => p.Minimum.ToString(CultureInfo.CurrentCulture),
            IntegerRange p => p.Minimum.ToString(),
            IntegerStepRange p => p.Minimum.ToString(),
            LongRange p => p.Minimum.ToString(),
            LongStepRange p => p.Minimum.ToString(),
            StringValues p => string.Empty,
            null => string.Empty,
            _ => throw new NotImplementedException()
        };

        var maximum = propertyInfo.PropertyType switch
        {
            BoolEnum p => string.Empty,
            DoubleRange p => p.Maximum.ToString(CultureInfo.CurrentCulture),
            DoubleStepRange p => p.Maximum.ToString(CultureInfo.CurrentCulture),
            FloatRange p => p.Maximum.ToString(CultureInfo.CurrentCulture),
            FloatStepRange p => p.Maximum.ToString(CultureInfo.CurrentCulture),
            IntegerRange p => p.Maximum.ToString(),
            IntegerStepRange p => p.Maximum.ToString(),
            LongRange p => p.Maximum.ToString(),
            LongStepRange p => p.Maximum.ToString(),
            StringValues p => string.Empty,
            null => string.Empty,
            _ => throw new NotImplementedException()
        };

        var step = propertyInfo.PropertyType switch
        {
            BoolEnum p => string.Empty,
            DoubleRange p => string.Empty,
            DoubleStepRange p => p.Step.ToString(CultureInfo.CurrentCulture),
            FloatRange p => string.Empty,
            FloatStepRange p => p.Step.ToString(CultureInfo.CurrentCulture),
            IntegerRange p => string.Empty,
            IntegerStepRange p => p.Step.ToString(),
            LongRange p => string.Empty,
            LongStepRange p => p.Step.ToString(),
            StringValues p => string.Empty,
            null => string.Empty,
            _ => throw new NotImplementedException()
        };

        return new()
        {
            Name = propertyInfo.Name,
            Type = type,
            Default = @default,
            Minimum = minimum,
            Maximum = maximum,
            Step = step,
            IsParam = propertyInfo.IsParam
        };
    }
}