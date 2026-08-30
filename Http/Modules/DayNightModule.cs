using System;
using System.Threading;
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

static class MutateHttp
{
    public static HttpResponse OnMainThread(Func<HttpResponse> work)
    {
        var snap = new Snapshot();
        try
        {
            HttpResponse Wrapped()
            {
                var game = GameFacade.Current;
                snap.Phase = game.World.Phase;
                snap.Generation = game.Ids.SceneGeneration;
                Volatile.Write(ref snap.Ready, 1);
                return work();
            }

            var mt = MainThread.Current;
            return mt == null ? Wrapped() : mt.Run(Wrapped).GetAwaiter().GetResult();
        }
        catch (MainThreadTimeoutException)
        {
            ReadSnap(snap, out var phase, out var generation);
            return Json.Error(504, ErrorCodes.MainThreadTimeout, "main thread timed out", phase, generation);
        }
        catch (Exception ex)
        {
            ReadSnap(snap, out var phase, out var generation);
            return Json.Error(500, ErrorCodes.UnityException, ex.Message, phase, generation);
        }
    }

    public static bool IsJsonParseError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException!)
        {
            var name = e.GetType().Name;
            if (name.IndexOf("Json", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public static HttpResponse InvalidJson(Exception ex) =>
        Json.Error(400, "invalid_json", "request body is not valid JSON: " + ex.Message);

    static void ReadSnap(Snapshot snap, out string? phase, out int? generation)
    {
        if (Volatile.Read(ref snap.Ready) == 1)
        {
            phase = snap.Phase;
            generation = snap.Generation;
            return;
        }

        phase = null;
        generation = null;
    }

    sealed class Snapshot
    {
        public string? Phase;
        public int Generation;
        public int Ready;
    }
}
