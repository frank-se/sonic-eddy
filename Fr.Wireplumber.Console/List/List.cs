using System.CommandLine;

namespace Fr.Wireplumber.Console.List;

public static class List
{
    public static void BuildAndAttachListCommand(this Command rootCommand)
    {
        var command = new Command("list", "List pipewire objects");

        command.BuildAndAttachListDeviceCommand();

        rootCommand.Add(command);
    }
}