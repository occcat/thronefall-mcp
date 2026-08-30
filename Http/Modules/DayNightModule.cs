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
        catch (Exception ex)
        {
            return Json.Error(500, ErrorCodes.UnityException, ex.Message);
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

static class MutateHttp
{
    public static HttpResponse OnMainThread(Func<HttpResponse> work)
    {
        try
        {
            var mt = MainThread.Current;
            return mt == null ? work() : mt.Run(work).GetAwaiter().GetResult();
        }
        catch (MainThreadTimeoutException)
        {
            var game = GameFacade.Current;
            return Json.Error(
                504,
                ErrorCodes.MainThreadTimeout,
                "main thread timed out",
                game.World.Phase,
                game.Ids.SceneGeneration);
        }
        catch (Exception ex)
        {
            var game = GameFacade.Current;
            return Json.Error(
                500,
                ErrorCodes.UnityException,
                ex.Message,
                game.World.Phase,
                game.Ids.SceneGeneration);
        }
    }
}
