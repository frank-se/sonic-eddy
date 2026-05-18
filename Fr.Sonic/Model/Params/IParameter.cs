using Fr.Sonic.Marshalling;
using Fr.Sonic.Model.Params.Marshalling;

namespace Fr.Sonic.Model.Params;

/// <summary>
/// Interface to describe a pipewire parameter
/// </summary>
public interface IParameter
{
    /// <summary>
    /// Name of the parameter
    /// </summary>
    string Name { get; }

    internal static IParameter FromIntPtr(
        string name,
        ParamType type,
        IntPtr valuePtr)
    {
        switch (type)
        {
            case ParamType.Bool:
                var boolValue = valuePtr.ToInt64() != 0;
                return new Parameter<bool>(name, boolValue);
            case ParamType.Int:
                var intValue = (int)valuePtr.ToInt64();
                return new Parameter<int>(name, intValue);
            case ParamType.Long:
                var longValue = valuePtr.ToInt64();
                return new Parameter<long>(name, longValue);
            case ParamType.Float:
                var longValueTmp = valuePtr.ToInt64();
                var lowerHalf = (int)longValueTmp;
                var floatValue = BitConverter.Int32BitsToSingle(lowerHalf);
                return new Parameter<float>(name, floatValue);
            case ParamType.Double:
                var doubleValue =
                    BitConverter.Int64BitsToDouble(valuePtr.ToInt64());
                return new Parameter<double>(name, doubleValue);
            case ParamType.String:
                var stringValue = valuePtr.ConvertToString();
                return stringValue is not null
                    ? new Parameter<string>(name, stringValue)
                    : throw new ArgumentOutOfRangeException(nameof(type), type,
                        null);
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}