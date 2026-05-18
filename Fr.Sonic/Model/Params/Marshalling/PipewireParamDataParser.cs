using System.Runtime.InteropServices;
using Fr.Sonic.Marshalling;

namespace Fr.Sonic.Model.Params.Marshalling;

internal static class PipewireParamDataParser
{
    internal static Dictionary<string, IParameter> ParseParameterUpdates(
        IntPtr parametersPtr)
    {
        var result = new Dictionary<string, IParameter>();

        unsafe
        {
            var updates =
                Marshal.PtrToStructure<PipewireParamData>(parametersPtr);

            var count = (int)updates.count;

            var keys = new Span<IntPtr>(updates.keys.ToPointer(), count);
            var types =
                new Span<ParamType>(updates.types.ToPointer(), count);
            var values =
                new Span<IntPtr>(updates.values.ToPointer(), count);

            for (var i = 0; i < count; i++)
            {
                var name = keys[i].ConvertToString();
                if (name is null)
                    throw new ArgumentException("name cannot be null");
                result.Add(name,
                    IParameter.FromIntPtr(name, types[i], values[i]));
            }
        }

        return result;
    }
}