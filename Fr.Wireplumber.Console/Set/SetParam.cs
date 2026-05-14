using System.CommandLine;

namespace Fr.Wireplumber.Console.Set;

public static class SetParam
{
    public static void BuildAndAttachSetParamCommand(this Command rootCommand)
    {
        Option<ulong> nodeIdOption = new("--node-id", "-n")
        {
            Required = true,
            Description = "The node id"
        };

        Option<string> parameterNameOption = new("--parameter", "-p")
        {
            Required = true,
            Description = "Name of the param to set"
        };

        Option<float> parameterValueOption = new("--value", "-v")
        {
            Required = true,
            Description = "The new value for the parameter"
        };

        var command = new Command("param", "Set node param")
        {
            nodeIdOption,
            parameterNameOption,
            parameterValueOption
        };

        command.SetAction(async parseResult =>
        {
            var nodeId = parseResult.GetValue(nodeIdOption);
            var name = parseResult.GetValue(parameterNameOption);
            var value = parseResult.GetValue(parameterValueOption);

            System.Console.WriteLine($"Node Id {nodeId}");
            System.Console.WriteLine($"Name {name}");
            System.Console.WriteLine($"Value {value}");

            Wireplumber.Start();
            
            Wireplumber.NodeRegistry.Added += node =>
            {
                if (node.ObjectId == nodeId && name is not null)
                {
                    node.SetParam(name, value);
                }
            };
            
            await Task.Delay(TimeSpan.FromMinutes(5));
            return 0;
        });
        
        rootCommand.Add(command);
    }
}