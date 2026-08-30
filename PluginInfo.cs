using System;

namespace ThronefallControl;

public static class PluginInfo
{
    public const string Id = "com.thronefall.control";
    public const string Name = "Thronefall Control";
    public const string Version = "0.1.0";
    public const string GameVersion = "2.13";

    public static readonly DateTimeOffset StartedAtUtc = DateTimeOffset.UtcNow;

    public static double UptimeSeconds =>
        Math.Max(0, (DateTimeOffset.UtcNow - StartedAtUtc).TotalSeconds);
}
