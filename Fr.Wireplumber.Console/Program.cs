using System.CommandLine;
using Fr.Wireplumber.Console.Create;
using Fr.Wireplumber.Console.List;
using Fr.Wireplumber.Console.Set;

var rootCommand = new RootCommand("Console app for fr-wireplumber");

rootCommand.BuildAndAttachListCommand();
rootCommand.BuildAndAttachSetCommand();
rootCommand.BuildAndAttachCreateCommand();

var result = rootCommand.Parse(args);

return result.Invoke();