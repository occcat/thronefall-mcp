using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Http;
using ThronefallControl.Http.Modules;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class DebugModuleTests : IDisposable
{
    readonly bool _cheats;
    readonly bool _upgrade;
    readonly bool _save;

    public DebugModuleTests()
    {
        _cheats = PluginConfig.EnableDebugCheats;
        _upgrade = PluginConfig.EnableDebugUpgradeToMax;
        _save = PluginConfig.AllowSaveApi;
        PluginConfig.EnableDebugCheats = false;
        PluginConfig.EnableDebugUpgradeToMax = false;
        PluginConfig.AllowSaveApi = false;
        RuntimeState.Reset();
        DebugModule.Reset();
    }

    public void Dispose()
    {
        PluginConfig.EnableDebugCheats = _cheats;
        PluginConfig.EnableDebugUpgradeToMax = _upgrade;
        PluginConfig.AllowSaveApi = _save;
        RuntimeState.Reset();
        DebugModule.Reset();
    }

    [Theory]
    [InlineData("/debug/upgrade-max")]
    [InlineData("/debug/skip-wave")]
    [InlineData("/debug/invulnerable")]
    [InlineData("/debug/save")]
    public void Debug_is_403_when_flags_off(string path)
    {
        RuntimeState.Phase = Phases.Day;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            path,
            body: "{\"clientRequestId\":\"d-1\"}"));
        Assert.Equal(403, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.NotNull(err);
        Assert.False(err!.Ok);
        Assert.Equal(ErrorCodes.CheatDisabled, err.Error);
    }

    [Fact]
    public void Upgrade_max_stays_disabled_if_only_generic_cheats_on()
    {
        PluginConfig.EnableDebugCheats = true;
        PluginConfig.EnableDebugUpgradeToMax = false;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("POST", "/debug/upgrade-max", body: "{}"));
        Assert.Equal(403, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.CheatDisabled, err!.Error);
    }
}