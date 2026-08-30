using System;
using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class HealthModule : IRouteModule
{
    public static Func<int>? FrameCountReader { get; set; }

    public void Register(Router router)
    {
        router.Map("GET", "/health", Alive);
        router.Map("GET", "/health/ready", Ready);
    }

    static HttpResponse Alive(RequestContext ctx)
    {
        _ = ctx;
        return Json.Ok(Snapshot(ready: false, frameCount: 0));
    }

    static HttpResponse Ready(RequestContext ctx)
    {
        _ = ctx;
        var mt = MainThread.Current;
        if (mt == null)
            return Json.Ok(Snapshot(ready: false, frameCount: 0));

        try
        {
            var body = mt.Run(() => Snapshot(ready: true, frameCount: ReadFrameCount()))
                .GetAwaiter()
                .GetResult();
            return Json.Ok(body);
        }
        catch (MainThreadTimeoutException ex)
        {
            return Json.Error(504, ErrorCodes.MainThreadTimeout, ex.Message);
        }
        catch (Exception ex)
        {
            return Json.Error(500, ErrorCodes.UnityException, ex.Message);
        }
    }

    static HealthResponse Snapshot(bool ready, int frameCount) => new()
    {
        Ok = true,
        Plugin = "ThronefallControl",
        Version = PluginInfo.Version,
        GameVersion = PluginInfo.GameVersion,
        Bound = $"{PluginConfig.BindAddress}:{PluginConfig.HttpPort}",
        Phase = null,
        Generation = 0,
        Scene = null,
        CheatsEnabled = PluginConfig.EnableDebugCheats,
        UptimeSeconds = PluginInfo.UptimeSeconds,
        Ready = ready,
        FrameCount = frameCount
    };

    static int ReadFrameCount()
    {
        try
        {
            return FrameCountReader?.Invoke() ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
