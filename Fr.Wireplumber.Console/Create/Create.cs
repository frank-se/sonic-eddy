using System.CommandLine;

namespace Fr.Wireplumber.Console.Create;

public static class Create
{
    public static void BuildAndAttachCreateCommand(this Command rootCommand)
    {
        var command = new Command("create", "Create pipewire objects");
        
        command.BuildAndAttachCreateLinkCommand();
        
        rootCommand.Add(command);
    }
}