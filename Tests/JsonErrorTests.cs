using ThronefallControl.Dto;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class JsonErrorTests
{
    [Fact]
    public void Error_envelope_matches_design()
    {
        var res = Json.Error(409, ErrorCodes.StaleId, "stale slot id", phase: "day", generation: 4);
        Assert.Equal(409, res.Status);
        var body = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.NotNull(body);
        Assert.False(body!.Ok);
        Assert.Equal(ErrorCodes.StaleId, body.Error);
        Assert.Equal("stale slot id", body.Message);
        Assert.Equal("day", body.Phase);
        Assert.Equal(4, body.Generation);
        Assert.Contains("\"ok\":false", res.Body.Replace(" ", ""));
        Assert.Contains("\"error\":\"stale_id\"", res.Body.Replace(" ", ""));
    }

    [Fact]
    public void Ok_body_uses_camel_case()
    {
        var res = Json.Ok(new HealthResponse { Ok = true, Plugin = "ThronefallControl" });
        Assert.Contains("\"ok\":true", res.Body.Replace(" ", ""));
        Assert.Contains("\"plugin\":\"ThronefallControl\"", res.Body.Replace(" ", ""));
    }
}
