using Fr.Sonic;
using Fr.Sonic.Monitoring;

Console.WriteLine("Fr.Sonic Console — unified backend test");

FrSonic.Init(TimeSpan.FromMilliseconds(100));
FrSonic.Start();

/* ── wireplumber / object model ──────────────────────────────────────────── */

FrSonic.NodeRegistry.Added += node =>
{
    Console.WriteLine($"[node+] serial={node.ObjectSerial} name={node.Name} class={node.Media.Class}");
};

FrSonic.NodeRegistry.Deleted += node =>
{
    Console.WriteLine($"[node-] serial={node.ObjectSerial} name={node.Name}");
};

FrSonic.DeviceRegistry.Added += device =>
{
    Console.WriteLine($"[device+] serial={device.ObjectSerial} name={device.Name}");
};

FrSonic.PortRegistry.Added += port =>
{
    Console.WriteLine($"[port+] serial={port.ObjectSerial} name={port.Name} dir={port.Direction}");
};

/* ── monitoring ──────────────────────────────────────────────────────────── */

FrSonicMonitoring.PeakChanged += (_, update) =>
{
    Console.WriteLine(
        $"[peak] serial={update.ObjectSerial} " +
        $"L={update.LeftPeak:F3} R={update.RightPeak:F3}");
};

/* ── midi ────────────────────────────────────────────────────────────────── */

FrSonicMidi.LayerChanged += args =>
    Console.WriteLine($"[midi] layer={args.LayerId}");

FrSonicMidi.SelectedChannelChanged += args =>
    Console.WriteLine($"[midi] channel type={args.ChannelType} id={args.ChannelId}");

FrSonicMidi.DialSelectionModeChanged += args =>
    Console.WriteLine($"[midi] dial mode={args.DialMode} ch={args.ChannelId}");

FrSonicMidi.MidiControlChangeUpdate += update =>
    Console.WriteLine($"[midi cc] ch={update.ChannelId} obj={update.ObjectId} " +
                      $"param={update.ParameterName} val={update.NormalizedControllerValue:F3}");

/* ── lv2 ─────────────────────────────────────────────────────────────────── */

FrSonicLv2.Init();
var plugins = System.Text.Json.JsonSerializer.Deserialize<List<Fr.Sonic.Model.Lv2.PluginDescription>>(
    FrSonicLv2.PluginDescriptionsJson() ?? "[]");
Console.WriteLine($"[lv2] {plugins?.Count ?? 0} plugins discovered");
FrSonicLv2.Destroy();

/* ── run ─────────────────────────────────────────────────────────────────── */

Console.WriteLine("Running — press Ctrl+C to stop");
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { }

FrSonic.Stop();
Console.WriteLine("Stopped.");
