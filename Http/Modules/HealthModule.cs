using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Http.Modules;

public sealed class HealthModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("GET", "/health", Alive);
    }

    static HttpResponse Alive(RequestContext ctx)
    {
        _ = ctx;
        return Json.Ok(new HealthResponse
        {
            Ok = true,
            Plugin = "ThronefallControl",
            Version = ThronefallControl.PluginInfo.Version,
            GameVersion = ThronefallControl.PluginInfo.GameVersion,
            Bound = $"{PluginConfig.BindAddress}:{PluginConfig.HttpPort}",
            Phase = null,
            Generation = 0,
            Scene = null,
            CheatsEnabled = PluginConfig.EnableDebugCheats,
            UptimeSeconds = ThronefallControl.PluginInfo.UptimeSeconds
        });
    }
}
