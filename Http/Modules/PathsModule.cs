using System;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class PathsModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("POST", "/path/toggle", Handle);
    }

    static HttpResponse Handle(RequestContext ctx)
    {
        TogglePathRequest req;
        try
        {
            req = Parse(ctx);
        }
        catch (Exception ex) when (MutateHttp.IsJsonParseError(ex))
        {
            return MutateHttp.InvalidJson(ex);
        }

        var dryRun = ctx.DryRun || req.DryRun;
        return MutateHttp.OnMainThread(() => From(Paths.Toggle(req.Id, dryRun)));
    }

    static TogglePathRequest Parse(RequestContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Body))
            return new TogglePathRequest();
        return Json.Deserialize<TogglePathRequest>(ctx.Body) ?? new TogglePathRequest();
    }

    static HttpResponse From(TogglePathResult result)
    {
        if (result.Error != null)
        {
            return Json.Error(
                result.Status,
                result.Error,
                result.Message,
                result.Phase,
                result.Generation);
        }

        if (result.DryRun)
        {
            return Json.Ok(new DryRunResponse
            {
                Ok = true,
                DryRun = true,
                Would = result.Would ?? new DryRunWouldDto { Action = "toggle_path" }
            });
        }

        return Json.Ok(new TogglePathResponse
        {
            Ok = true,
            PathOpened = result.PathOpened,
            ToggleCost = result.ToggleCost,
            Generation = result.Generation,
            Id = result.Id
        });
    }
}
