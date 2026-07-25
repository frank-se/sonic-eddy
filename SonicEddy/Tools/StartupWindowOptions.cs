using System;

namespace SonicEddy.Tools;

/// <summary>
/// Parses which top-level windows to open at startup, e.g. for pinning each one to its
/// own sway workspace/output via app_id-based `for_window` rules. With no flags given,
/// only the main mixer window opens (matches pre-flag behavior).
/// </summary>
internal static class StartupWindowOptions
{
    public static (bool MainMixer, bool DrumMixer, bool Overview, bool GlobalMaster) Parse(
        string[] args)
    {
        var mainMixer = Contains(args, "--main-mixer");
        var drumMixer = Contains(args, "--drum-mixer");
        var overview = Contains(args, "--overview");
        var globalMaster = Contains(args, "--global-master");

        if (!mainMixer && !drumMixer && !overview && !globalMaster)
            mainMixer = true;

        return (mainMixer, drumMixer, overview, globalMaster);
    }

    private static bool Contains(string[] args, string flag) =>
        Array.IndexOf(args, flag) >= 0;
}
