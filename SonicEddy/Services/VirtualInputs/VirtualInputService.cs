using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Objects;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.VirtualInputs;

public class VirtualInputService(
    IAppDataService appDataService,
    IWireplumberService wireplumberService)
    : IVirtualInputService
{
    private const string CaptureNodeMediaClass = "Stream/Input/Audio";
    private const string PlaybackNodeMediaClass = "Stream/Output/Audio";
    private static readonly List<string> StereoAudioPosition = ["FL", "FR"];

    public List<VirtualInput> VirtualInputs { get; } = [];

    public async Task AddVirtualInput(string name, Node node, Port[] ports)
    {
        var fullName = $"virtual-input-{name}";

        var captureNodeAudioPosition =
            ports.Select(p => p.Channel).OfType<string>().ToList();

        var loopback = await wireplumberService.CreateLoopbackModule(
            fullName, new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"{fullName}-capture",
                    Description =
                        $"{fullName}-capture",
                    DontFallback = true,
                    MediaClass = CaptureNodeMediaClass,
                    TargetObject = node.ObjectSerial.ToString(),
                    AudioPosition = captureNodeAudioPosition
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"{fullName}-playback",
                    Description =
                        $"{fullName}-playback",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = PlaybackNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false,
                }
            });

        var virtualInput = new VirtualInput(name, node, ports, loopback);
        
        VirtualInputs.Add(virtualInput);
        
        Added?.Invoke(virtualInput);
    }

    public event Action<VirtualInput>? Added;
}