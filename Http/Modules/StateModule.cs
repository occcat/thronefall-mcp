using System;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class StateModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("GET", "/state", GetState);
        router.Map("GET", "/state/slots", ctx => GetSlice(ctx, StateInclude.Slots));
        router.Map("GET", "/state/units", ctx => GetSlice(ctx, StateInclude.Units));
        router.Map("GET", "/state/training", ctx => GetSlice(ctx, StateInclude.Training));
        router.Map("GET", "/state/enemies", ctx => GetSlice(ctx, StateInclude.Enemies));
        router.Map("GET", "/state/spawns", ctx => GetSlice(ctx, StateInclude.Spawns));
        router.Map("GET", "/state/next-wave", ctx => GetSlice(ctx, StateInclude.NextWave));
        router.Map("GET", "/state/loadout", ctx => GetSlice(ctx, StateInclude.Loadout));
    }

    static HttpResponse GetState(RequestContext ctx)
    {
        ctx.Query.TryGetValue("include", out var include);
        return OnMain(() =>
        {
            var dto = GameFacade.Current.GetState(include);
            return Json.Ok(dto);
        });
    }

    static HttpResponse GetSlice(RequestContext ctx, string slice)
    {
        _ = ctx;
        return OnMain(() =>
        {
            var facade = GameFacade.Current;
            if (!facade.TryGetSlice(slice, out var dto, out var error, out var message))
            {
                return Json.Error(
                    409,
                    error ?? ErrorCodes.IllegalPhase,
                    message ?? $"GET /state/{slice} is illegal in phase={facade.Phase}",
                    facade.Phase,
                    facade.Ids.SceneGeneration);
            }

            return Json.Ok(dto!);
        });
    }

    static HttpResponse OnMain(Func<HttpResponse> work)
    {
        var mt = MainThread.Current;
        if (mt == null)
            return Invoke(work);

        try
        {
            var task = mt.Run(work);
            return task.GetAwaiter().GetResult();
        }
        catch (MainThreadTimeoutException)
        {
            return Json.Error(504, ErrorCodes.MainThreadTimeout, "main thread timeout");
        }
        catch (AggregateException ae) when (ae.InnerException is MainThreadTimeoutException)
        {
            return Json.Error(504, ErrorCodes.MainThreadTimeout, "main thread timeout");
        }
        catch (Exception ex)
        {
            return Json.Error(500, ErrorCodes.UnityException, ex.Message);
        }
    }

    static HttpResponse Invoke(Func<HttpResponse> work)
    {
        try
        {
            return work();
        }
        catch (Exception ex)
        {
            return Json.Error(500, ErrorCodes.UnityException, ex.Message);
        }
    }
}
