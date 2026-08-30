using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class ServerTests
{
    [Fact]
    public void Process_serves_health_alive()
    {
        var previous = PluginConfig.AuthToken;
        PluginConfig.AuthToken = "";
        try
        {
            var server = new Server();
            var res = server.Process(RequestContext.Create("GET", "/health"));
            Assert.Equal(200, res.Status);
            var body = Json.Deserialize<HealthResponse>(res.Body);
            Assert.True(body!.Ok);
        }
        finally
        {
            PluginConfig.AuthToken = previous;
        }
    }

    [Fact]
    public void Process_enforces_auth_before_router()
    {
        var previous = PluginConfig.AuthToken;
        PluginConfig.AuthToken = "secret";
        try
        {
            var server = new Server();
            var res = server.Process(RequestContext.Create("GET", "/health"));
            Assert.Equal(401, res.Status);
        }
        finally
        {
            PluginConfig.AuthToken = previous;
        }
    }

    [Fact]
    public void Start_rejects_non_loopback_without_throwing()
    {
        var previous = PluginConfig.BindAddress;
        PluginConfig.BindAddress = "0.0.0.0";
        try
        {
            var server = new Server();
            server.Start();
            Assert.False(server.IsListening);
            Assert.False(Server.IsLoopback("0.0.0.0"));
            Assert.False(Server.IsLoopback("*"));
            Assert.True(Server.IsLoopback("127.0.0.1"));
            Assert.True(Server.IsLoopback("::1"));
        }
        finally
        {
            PluginConfig.BindAddress = previous;
        }
    }

    [Fact]
    public void Cheats_are_off_by_default()
    {
        Assert.False(PluginConfig.EnableDebugCheats);
        Assert.False(PluginConfig.EnableDebugUpgradeToMax);
        Assert.False(PluginConfig.AllowSaveApi);
        Assert.False(PluginConfig.UseCommandUnitsSolver);
        Assert.Equal(NightPolicies.Human, PluginConfig.DefaultNightPolicy);
    }
}
