using System;
using System.Globalization;
using System.Reflection;

namespace ThronefallControl.Config;

public static class PluginConfig
{
    public static string BindAddress { get; set; } = "127.0.0.1";
    public static int HttpPort { get; set; } = 17891;
    public static string AuthToken { get; set; } = "";

    public static bool EnableDebugCheats { get; set; }
    public static bool EnableDebugUpgradeToMax { get; set; }
    public static bool AllowSaveApi { get; set; }
    public static bool RefuseMutateDuringTransition { get; set; } = true;

    public static bool UseCommandUnitsSolver { get; set; }
    public static float WallBackOffset { get; set; } = 3f;
    public static int MainThreadTimeoutMs { get; set; } = 500;
    public static int MaxWorkItemsPerFrame { get; set; } = 8;

    public static string DefaultNightPolicy { get; set; } = "human";

    public static void Bind(object? config = null)
    {
        if (config == null)
            return;

        BindAddress = BindEntry(config, "Http", "BindAddress", BindAddress,
            "Loopback address for HttpListener. Non-loopback values are rejected.");
        HttpPort = BindEntry(config, "Http", "HttpPort", HttpPort,
            "HTTP port. Default 17891.");
        AuthToken = BindEntry(config, "Http", "AuthToken", AuthToken,
            "If non-empty, require header X-Thronefall-Token.");

        EnableDebugCheats = BindEntry(config, "Safety", "EnableDebugCheats", EnableDebugCheats,
            "Master switch for debug endpoints. Off by default.");
        EnableDebugUpgradeToMax = BindEntry(config, "Safety", "EnableDebugUpgradeToMax", EnableDebugUpgradeToMax,
            "Allow POST /debug/upgrade-max. Off by default.");
        AllowSaveApi = BindEntry(config, "Safety", "AllowSaveApi", AllowSaveApi,
            "Allow POST /debug/save. Off by default.");
        RefuseMutateDuringTransition = BindEntry(config, "Safety", "RefuseMutateDuringTransition",
            RefuseMutateDuringTransition,
            "Return 409 transition_in_progress while a scene change is running.");

        UseCommandUnitsSolver = BindEntry(config, "Units", "UseCommandUnitsSolver", UseCommandUnitsSolver,
            "Use CommandUnits placement solver. v1 default is fallback.");
        WallBackOffset = BindEntry(config, "Units", "WallBackOffset", WallBackOffset,
            "Meters to stand off walls when posting to a spawn line.");
        MainThreadTimeoutMs = BindEntry(config, "Units", "MainThreadTimeoutMs", MainThreadTimeoutMs,
            "HTTP wait for Plugin.Update to run queued work, in milliseconds.");
        MaxWorkItemsPerFrame = BindEntry(config, "Units", "MaxWorkItemsPerFrame", MaxWorkItemsPerFrame,
            "Max queued work items drained per Unity frame.");

        DefaultNightPolicy = BindEntry(config, "Night", "DefaultPolicy", DefaultNightPolicy,
            "Night execution policy: human, afk_castle, or scripted_posts.");
    }

    static T BindEntry<T>(object config, string section, string key, T defaultValue, string description)
    {
        try
        {
            foreach (var method in config.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Bind" || !method.IsGenericMethodDefinition)
                    continue;
                var ps = method.GetParameters();
                if (ps.Length != 4)
                    continue;
                if (ps[0].ParameterType != typeof(string) || ps[1].ParameterType != typeof(string))
                    continue;
                if (ps[3].ParameterType != typeof(string))
                    continue;

                var entry = method.MakeGenericMethod(typeof(T)).Invoke(
                    config, new object?[] { section, key, defaultValue, description });
                if (entry == null)
                    return defaultValue;

                var value = entry.GetType().GetProperty("Value")?.GetValue(entry);
                if (value is T typed)
                    return typed;
                if (value == null)
                    return defaultValue;
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // A bad config file must not take the plugin down with Awake.
        }

        return defaultValue;
    }
}
