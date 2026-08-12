using System;

namespace SonicEddy.Tools;

/// <summary>
/// Parses which top-level windows to open at startup, e.g. for pinning each one to its
/// own sway workspace/output via app_id-based `for_window` rules. With no flags given,
/// only the main mixer window opens (matches pre-flag behavior).
/// </summary>
internal static class StartupWindowOptions
{
    public static (bool MainMixer, bool DrumMixer, bool Overview, bool GlobalMaster, bool MicChannel, bool GlobalReturnChannels) Parse(
        string[] args)
    {
        var mainMixer = Contains(args, "--main-mixer");
        var drumMixer = Contains(args, "--drum-mixer");
        var overview = Contains(args, "--overview");
        var globalMaster = Contains(args, "--global-master");
        var micChannel = Contains(args, "--mic-channel");
        var globalReturnChannels = Contains(args, "--global-return-channels");

        if (!mainMixer && !drumMixer && !overview && !globalMaster &&
            !micChannel && !globalReturnChannels)
            mainMixer = true;

        return (mainMixer, drumMixer, overview, globalMaster, micChannel,
            globalReturnChannels);
    }

    /// <summary>
    /// Returns the value passed after `--mixer-name`, or null if the flag was not given.
    /// When set, the app loads the saved mixer config with that name once the mixer is
    /// ready at startup, creating a new one if none exists yet.
    /// </summary>
    public static string? ParseMixerName(string[] args)
    {
        var index = Array.IndexOf(args, "--mixer-name");
        if (index < 0 || index + 1 >= args.Length)
            return null;

        return args[index + 1];
    }

    private static bool Contains(string[] args, string flag) =>
        Array.IndexOf(args, flag) >= 0;
}
