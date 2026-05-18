using System.Runtime.InteropServices;

namespace Fr.Sonic.Model.Params.Marshalling;

[StructLayout(LayoutKind.Sequential)]
internal struct PipewireParamData()
{
    public ulong count = 0;
    public IntPtr keys;
    public IntPtr types;
    public IntPtr values;
}