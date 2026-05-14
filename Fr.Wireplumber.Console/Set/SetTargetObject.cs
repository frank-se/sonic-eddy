using System.CommandLine;

namespace Fr.Wireplumber.Console.Set;

public static class SetTargetObject
{
    public static void BuildAndAttachSetTargetObjectCommand(
        this Command rootCommand)
    {
        Option<ulong> nodeIdOption = new("--node-id", "-n")
        {
            Required = true,
            Description = "The node id"
        };

        Option<string> targetObjectOption = new("--target-object", "-t")
        {
            Required = true,
            Description = "The target object"
        };

        var command = new Command("target-object", "Set target object")
        {
            nodeIdOption,
            targetObjectOption
        };

        command.SetAction(async parseResult =>
        {
            var nodeId = parseResult.GetValue(nodeIdOption);
            var targetObject = parseResult.GetValue(targetObjectOption);

            System.Console.WriteLine($"Node Id {nodeId}");
            System.Console.WriteLine($"Target Object: {targetObject}");

            Wireplumber.Start();

            Wireplumber.NodeRegistry.Added += node =>
            {
                if (node.ObjectId == nodeId && targetObject is not null)
                {
                    node.OverrideTargetObject(targetObject);
                }
            };

            await Task.Delay(TimeSpan.FromMinutes(5));
            return 0;
        });
        
        rootCommand.Add(command);
    }
}