using System.CommandLine;

namespace Fr.Wireplumber.Console.Set;

public static class Set
{
    public static void BuildAndAttachSetCommand(this Command rootCommand)
    {
        var command = new Command("set", "Set pipewire parameters");

        command.BuildAndAttachSetParamCommand();
        command.BuildAndAttachSetTargetObjectCommand();
        
        rootCommand.Add(command);
    }
}