using System.Text.Json.Serialization;

namespace Fr.Sonic.Model.PropInfo;

/// <summary>
/// Absract class to describe a pipewire property for the property info.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(IntegerRange), "IntegerRange")]
[JsonDerivedType(typeof(LongRange), "LongRange")]
[JsonDerivedType(typeof(FloatRange), "FloatRange")]
[JsonDerivedType(typeof(DoubleRange), "DoubleRange")]
[JsonDerivedType(typeof(BoolEnum), "BoolEnum")]
[JsonDerivedType(typeof(StringValues), "StringValues")]
[JsonDerivedType(typeof(IntegerStepRange), "IntegerStepRange")]
[JsonDerivedType(typeof(LongStepRange), "LongStepRange")]
[JsonDerivedType(typeof(FloatStepRange), "FloatStepRange")]
[JsonDerivedType(typeof(DoubleStepRange), "DoubleStepRange")]
public abstract class PropertyType
{
}