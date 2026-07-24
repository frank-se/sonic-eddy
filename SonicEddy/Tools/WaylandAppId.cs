using System;
using System.Reflection;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using NWayland.Protocols.XdgShell;

namespace SonicEddy.Tools;

/// <summary>
/// Workaround for Avalonia.Wayland (12.1.0) never calling xdg_toplevel.set_app_id:
/// reaches into its internal WindowImpl -> WXdgTopLevelProxy -> XdgToplevel chain via
/// reflection and invokes SetAppId directly, marshalled onto the Wayland worker thread
/// the same way Avalonia's own SetTitle etc. do. Tracked upstream at
/// https://github.com/AvaloniaUI/Avalonia - remove once app_id support lands there.
/// </summary>
internal static class WaylandAppId
{
    private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void TrySet(TopLevel topLevel, string appId, ILogger? logger = null)
    {
        try
        {
            var windowImpl = topLevel.PlatformImpl;
            if (windowImpl is null ||
                windowImpl.GetType().FullName != "Avalonia.Wayland.WindowImpl")
            {
                return;
            }

            var surfaceProxy = FindField(windowImpl.GetType(), "_surfaceProxy")
                ?.GetValue(windowImpl);
            if (surfaceProxy is null) return;

            var target = FindField(surfaceProxy.GetType(), "_target")?.GetValue(surfaceProxy);
            if (target is null) return;

            if (FindField(target.GetType(), "_xdgTopLevel")?.GetValue(target)
                is not XdgToplevel xdgToplevel)
            {
                return;
            }

            if (FindField(surfaceProxy.GetType(), "_marshaller")?.GetValue(surfaceProxy)
                is not Delegate marshaller)
            {
                return;
            }

            var priorityType = marshaller.GetType().GetGenericArguments()[1];
            var normalPriority = Enum.Parse(priorityType, "Normal");

            marshaller.DynamicInvoke((Action)(() => xdgToplevel.SetAppId(appId)), normalPriority);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "Failed to set Wayland app_id via reflection workaround; " +
                "Avalonia.Wayland internals may have changed");
        }
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            var field = t.GetField(name, InstanceNonPublic);
            if (field is not null) return field;
        }

        return null;
    }
}
