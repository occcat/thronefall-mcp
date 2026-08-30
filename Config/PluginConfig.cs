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
        _ = config;
    }
}
