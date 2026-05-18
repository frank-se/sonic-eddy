namespace Fr.Sonic.Model.Params;

/// <summary>
/// A pipewire parameter
/// </summary>
/// <param name="Name">The name of the parameter</param>
/// <param name="Value">The value of the parameter</param>
/// <typeparam name="T"></typeparam>
public record Parameter<T>(string Name, T Value) : IParameter;