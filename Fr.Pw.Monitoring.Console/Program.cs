using System.CommandLine;
using Fr.Pw.Monitoring.Console.Monitor;

var rootCommand = new RootCommand("Console app to monitor pipewire nodes");

rootCommand.BuildAndAttachMonitorCommand();

var result = rootCommand.Parse(args);

return result.Invoke();