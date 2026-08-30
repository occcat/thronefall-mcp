using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
        Assert.Equal("127.0.0.1", PluginConfig.BindAddress);
        Assert.Equal(17891, PluginConfig.HttpPort);
        Assert.Equal(500, PluginConfig.MainThreadTimeoutMs);
        Assert.Equal("", PluginConfig.AuthToken);
    }

    [Fact]
    public async Task Start_listens_on_loopback_and_serves_health()
    {
        using var restore = ConfigRestore.Capture();
        PluginConfig.AuthToken = "";
        PluginConfig.BindAddress = "127.0.0.1";
        using var server = new Server();
        Assert.True(TryStartOnFreePort(server));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var sw = Stopwatch.StartNew();
        using var res = await client.GetAsync($"http://127.0.0.1:{PluginConfig.HttpPort}/health");
        sw.Stop();
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = Json.Deserialize<HealthResponse>(await res.Content.ReadAsStringAsync());
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.Equal("ThronefallControl", body.Plugin);
        Assert.False(body.Ready);
        Assert.False(body.CheatsEnabled);
        Assert.True(sw.ElapsedMilliseconds < 400, $"alive /health took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Start_returns_401_without_token_when_configured()
    {
        using var restore = ConfigRestore.Capture();
        PluginConfig.AuthToken = "secret";
        PluginConfig.BindAddress = "127.0.0.1";
        using var server = new Server();
        Assert.True(TryStartOnFreePort(server));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var missing = await client.GetAsync($"http://127.0.0.1:{PluginConfig.HttpPort}/health");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        var err = Json.Deserialize<ErrorResponse>(await missing.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.Unauthorized, err!.Error);

        using var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{PluginConfig.HttpPort}/health");
        req.Headers.TryAddWithoutValidation(Auth.HeaderName, "secret");
        using var ok = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public void Start_occupied_port_does_not_throw()
    {
        using var restore = ConfigRestore.Capture();
        PluginConfig.AuthToken = "";
        PluginConfig.BindAddress = "127.0.0.1";
        var port = FreePort();
        var blocker = new TcpListener(IPAddress.Loopback, port);
        blocker.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
        blocker.Start();
        try
        {
            PluginConfig.HttpPort = port;
            using var server = new Server();
            server.Start();
            Assert.False(server.IsListening);
        }
        finally
        {
            blocker.Stop();
        }
    }

    internal static bool TryStartOnFreePort(Server server)
    {
        for (var i = 0; i < 8; i++)
        {
            PluginConfig.HttpPort = FreePort();
            server.Start();
            if (server.IsListening)
                return true;
            server.Stop();
        }

        return false;
    }

    internal static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class ConfigRestore : IDisposable
{
    readonly string _bind;
    readonly int _port;
    readonly string _token;

    ConfigRestore(string bind, int port, string token)
    {
        _bind = bind;
        _port = port;
        _token = token;
    }

    public static ConfigRestore Capture() =>
        new(PluginConfig.BindAddress, PluginConfig.HttpPort, PluginConfig.AuthToken);

    public void Dispose()
    {
        PluginConfig.BindAddress = _bind;
        PluginConfig.HttpPort = _port;
        PluginConfig.AuthToken = _token;
    }
}
