using System.CommandLine;
using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Console.Create;

public static class CreateLink
{
    public static void BuildAndAttachCreateLinkCommand(this Command rootCommand)
    {
        Option<ulong> outputPortIdOption = new("--output-port-id", "-o")
        {
            Required = true,
            Description = "The output port to start the link at"
        };

        Option<ulong> inputPortIdOption = new("--input-port-id", "-i")
        {
            Required = true,
            Description = "The input port to connect the link to"
        };

        var command = new Command("link", "Create a link between two ports")
        {
            outputPortIdOption,
            inputPortIdOption
        };

        command.SetAction(async parseResult =>
        {
            var outputPortId = parseResult.GetValue(outputPortIdOption);
            var inputPortId = parseResult.GetValue(inputPortIdOption);

            System.Console.WriteLine(
                $"Connecting {outputPortId} to {inputPortId}");

            Port? outputPort = null;
            Port? inputPort = null;

            Wireplumber.Start();

            var created = false;
            
            Wireplumber.PortRegistry.Added += port =>
            {
                if (port.ObjectId == outputPortId)
                {
                    outputPort = port;
                }

                if (port.ObjectId == inputPortId)
                {
                    inputPort = port;
                }

                if (outputPort is null || inputPort is null || created) return;

                System.Console.WriteLine("Creating link");

                Fr.Wireplumber.Wireplumber.LinkFactory.CreateLink(
                    outputPort, inputPort);

                created = true;
            };

            await Task.Delay(TimeSpan.FromMinutes(2));
            return 0;
        });
        
        rootCommand.Add(command);
    }
}