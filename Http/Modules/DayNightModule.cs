using System;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class DayNightModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("POST", "/night/call", Handle);
    }

    static HttpResponse Handle(RequestContext ctx)
    {
        CallNightRequest req;
        try
        {
            req = Parse(ctx);
        }
        catch (Exception ex) when (MutateHttp.IsJsonParseError(ex))
        {
            return MutateHttp.InvalidJson(ex);
        }

        var dryRun = ctx.DryRun || req.DryRun;
        return MutateHttp.OnMainThread(() => From(DayNight.CallNight(dryRun)));
    }

    static CallNightRequest Parse(RequestContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Body))
            return new CallNightRequest();
        return Json.Deserialize<CallNightRequest>(ctx.Body) ?? new CallNightRequest();
    }

    static HttpResponse From(CallNightResult result)
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
                Would = result.Would ?? new DryRunWouldDto { Action = "call_night" }
            });
        }

        return Json.Ok(new CallNightResponse
        {
            Ok = true,
            Called = result.Called,
            Phase = result.Phase,
            Generation = result.Generation
        });
    }
}
