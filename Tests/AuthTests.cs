using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class AuthTests
{
    [Fact]
    public void Empty_token_allows_request()
    {
        var previous = PluginConfig.AuthToken;
        PluginConfig.AuthToken = "";
        try
        {
            var ctx = RequestContext.Create("GET", "/health");
            Assert.True(Auth.TryAuthorize(ctx, out var error));
            Assert.Null(error);
        }
        finally
        {
            PluginConfig.AuthToken = previous;
        }
    }

    [Fact]
    public void Missing_header_is_unauthorized()
    {
        var previous = PluginConfig.AuthToken;
        PluginConfig.AuthToken = "secret";
        try
        {
            var ctx = RequestContext.Create("GET", "/health");
            Assert.False(Auth.TryAuthorize(ctx, out var error));
            Assert.NotNull(error);
            Assert.Equal(401, error!.Status);
            var body = Json.Deserialize<ErrorResponse>(error.Body);
            Assert.Equal(ErrorCodes.Unauthorized, body!.Error);
        }
        finally
        {
            PluginConfig.AuthToken = previous;
        }
    }

    [Fact]
    public void Matching_header_is_authorized()
    {
        var previous = PluginConfig.AuthToken;
        PluginConfig.AuthToken = "secret";
        try
        {
            var ctx = RequestContext.Create(
                "GET",
                "/health",
                new Dictionary<string, string> { [Auth.HeaderName] = "secret" });
            Assert.True(Auth.TryAuthorize(ctx, out var error));
            Assert.Null(error);
        }
        finally
        {
            PluginConfig.AuthToken = previous;
        }
    }

    [Fact]
    public void Wrong_token_is_unauthorized()
    {
        var previous = PluginConfig.AuthToken;
        PluginConfig.AuthToken = "secret";
        try
        {
            var ctx = RequestContext.Create(
                "GET",
                "/health",
                new Dictionary<string, string> { [Auth.HeaderName] = "nope" });
            Assert.False(Auth.TryAuthorize(ctx, out var error));
            Assert.Equal(401, error!.Status);
        }
        finally
        {
            PluginConfig.AuthToken = previous;
        }
    }
}
