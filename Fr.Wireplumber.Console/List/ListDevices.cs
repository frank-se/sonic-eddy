using System.CommandLine;

namespace Fr.Wireplumber.Console.List;

public static class ListDevices
{
    public static void BuildAndAttachListDeviceCommand(
        this Command rootCommand)
    {
        var command = new Command("device", "List devices");
        command.SetAction(async parseResult =>
        {
            Wireplumber.Start();
            Wireplumber.DeviceRegistry.Added += device =>
            {
                System.Console.WriteLine($"Device: {device.ObjectSerial}");
                device.NodesListChanged += nodes =>
                {
                    foreach (var node in nodes)
                    {
                        System.Console.WriteLine($"Node: {node.ObjectSerial}");
                    }
                };
            };

            await Task.Delay(TimeSpan.FromMinutes(5));
            return 0;
        });
        rootCommand.Add(command);
    }
}