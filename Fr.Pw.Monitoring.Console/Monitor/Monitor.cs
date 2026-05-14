using System.CommandLine;

namespace Fr.Pw.Monitoring.Console.Monitor;

public static class Monitor
{
    public static void BuildAndAttachMonitorCommand(this Command rootCommand)
    {
        Option<ulong> outputPortIdOption = new("--object-serial", "-o")
        {
            Required = true,
            Description = "Object serial of the node to monitor"
        };

        var command = new Command("monitor", "Monitor the node")
        {
            outputPortIdOption
        };

        command.SetAction(async parseResult =>
        {
            var objectSerial = parseResult.GetValue(outputPortIdOption);

            System.Console.WriteLine(
                $"Monitoring object serial {objectSerial}");

            FrPwMonitoring.Start(TimeSpan.FromMilliseconds(500));

            var monitor = FrPwMonitoring.Monitor;

            monitor.Updated += (message) =>
            {
                System.Console.WriteLine(
                    $"Left peak: {message.Peaks[0]}, right peak: {message.Peaks[1]}");
            };

            monitor.StartMonitoring(objectSerial);

            await Task.Delay(TimeSpan.FromMinutes(2));
            return 0;
        });

        rootCommand.Add(command);
    }
}