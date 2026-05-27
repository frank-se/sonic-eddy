using System.Text.Json;
using Fr.Sonic.Model.Params;

namespace Fr.Sonic.Loopers;

internal static class LooperParamParser
{
    private const string MixKey = "mix";
    private const string CommandsKey = "commands";
    private const string StateKey = "looper.state";

    public static LooperParams? Parse(Dictionary<string, IParameter>? parameters)
    {
        if (parameters is null)
            return null;

        var hasLooperParam = false;
        float? mix = null;
        IReadOnlyList<LooperCommand> commands = [];
        LooperState? state = null;

        if (parameters.TryGetValue(MixKey, out var mixParameter))
        {
            hasLooperParam = true;
            mix = ParseMix(mixParameter);
        }

        if (parameters.TryGetValue(CommandsKey, out var commandsParameter))
        {
            hasLooperParam = true;
            commands = ParseCommands(commandsParameter);
        }

        if (parameters.TryGetValue(StateKey, out var stateParameter))
        {
            hasLooperParam = true;
            state = ParseState(stateParameter);
        }

        return hasLooperParam ? new(mix, commands, state) : null;
    }

    private static float? ParseMix(IParameter parameter) =>
        parameter switch
        {
            Parameter<float> value => value.Value,
            Parameter<double> value => (float)value.Value,
            Parameter<int> value => value.Value,
            Parameter<long> value => value.Value,
            _ => null
        };

    private static IReadOnlyList<LooperCommand> ParseCommands(
        IParameter parameter)
    {
        if (parameter is not Parameter<string> value ||
            string.IsNullOrWhiteSpace(value.Value))
            return [];

        try
        {
            using var document = JsonDocument.Parse(value.Value);
            var result = new List<LooperCommand>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array ||
                    item.GetArrayLength() < 2)
                    continue;

                result.Add(new(
                    item[0].GetUInt64(),
                    item[1].GetString() ?? string.Empty));
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static LooperState? ParseState(IParameter parameter)
    {
        if (parameter is not Parameter<string> value ||
            string.IsNullOrWhiteSpace(value.Value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<LooperState>(value.Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
