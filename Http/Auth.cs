using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Http;

public static class Auth
{
    public const string HeaderName = "X-Thronefall-Token";

    public static bool TryAuthorize(RequestContext ctx, out HttpResponse? error)
        => TryAuthorize(ctx, PluginConfig.AuthToken, out error);

    public static bool TryAuthorize(RequestContext ctx, string? token, out HttpResponse? error)
    {
        error = null;
        if (string.IsNullOrEmpty(token))
            return true;

        var provided = ctx.Header(HeaderName);
        if (provided == token)
            return true;

        error = Json.Error(401, ErrorCodes.Unauthorized,
            string.IsNullOrEmpty(provided) ? "missing token" : "invalid token");
        return false;
    }
}
