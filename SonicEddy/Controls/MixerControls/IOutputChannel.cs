using System.Windows.Input;

namespace SonicEddy.Controls.MixerControls;

public interface IOutputChannel : IChannel
{
    public ulong CaptureNodeObjectSerial { get; }
}