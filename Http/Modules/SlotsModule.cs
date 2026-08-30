using System;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class SlotsModule : IRouteModule
{
    readonly ISlotBackend? _backend;
    readonly IdempotencyCache _idempotency;
    readonly MainThread? _mainThread;

    public SlotsModule()
        : this(null, null, null)
    {
    }

    public SlotsModule(ISlotBackend? backend, IdempotencyCache? idempotency, MainThread? mainThread)
    {
        _backend = backend;
        _idempotency = idempotency ?? IdempotencyCache.Current ?? new IdempotencyCache();
        _mainThread = mainThread;
    }

    ISlotBackend? Backend => _backend ?? Slots.Backend;
    MainThread? Thread => _mainThread ?? MainThread.Current;

    public void Register(Router router)
    {
        router.Map("POST", "/slots/choice/cancel", Cancel);
        router.Map("POST", "/harvest", Harvest);
        router.Map("POST", "/slots/{id}/build", ctx => BuildOrUpgrade(ctx, "build"));
        router.Map("POST", "/slots/{id}/upgrade", ctx => BuildOrUpgrade(ctx, "upgrade"));
        router.Map("POST", "/slots/{id}/choice", Choice);
    }

    HttpResponse Harvest(RequestContext ctx)
    {
        var req = ReadHarvest(ctx);
        return WithIdempotency(req.ClientRequestId, () =>
        {
            var backend = RequireBackend(out var missing);
            if (missing != null)
                return missing;
            return ToHttp(Run(() => Game.Slots.Harvest(backend!, req.SlotId, req.Generation, req.DryRun || ctx.DryRun)));
        });
    }

    HttpResponse BuildOrUpgrade(RequestContext ctx, string action)
    {
        var req = ReadMutate(ctx);
        if (!TryParseId(ctx, out var instanceId, out var idError))
            return idError!;
        return WithIdempotency(req.ClientRequestId, () =>
        {
            var backend = RequireBackend(out var missing);
            if (missing != null)
                return missing;

            var dryRun = req.DryRun || ctx.DryRun;
            var result = Run(() => Game.Slots.BuildOrUpgrade(
                backend!, instanceId, req.Generation, dryRun, req.TeleportKingNearby, action));
            if (result.NeedsChoiceWait)
            {
                for (var i = 0; i < Game.Slots.ChoiceWaitFrames; i++)
                {
                    result = Run(() => Game.Slots.PollChoice(backend!, instanceId, req.Generation, action));
                    if (result.Payload is SlotMutateResponse body && body.IsWaitingForChoice)
                        break;
                }
            }

            return ToHttp(result);
        });
    }

    HttpResponse Choice(RequestContext ctx)
    {
        var req = ReadMutate(ctx);
        if (!TryParseId(ctx, out var instanceId, out var idError))
            return idError!;
        return WithIdempotency(req.ClientRequestId, () =>
        {
            var backend = RequireBackend(out var missing);
            if (missing != null)
                return missing;

            var dryRun = req.DryRun || ctx.DryRun;
            var result = Run(() => Game.Slots.Choose(backend!, instanceId, req.Generation, req.Name, dryRun));
            if (result.NeedsChoiceWait)
            {
                for (var i = 0; i < Game.Slots.ChoiceWaitFrames; i++)
                {
                    result = Run(() => Game.Slots.PollChoice(backend!, instanceId, req.Generation, "choice"));
                    var body = result.Payload as SlotMutateResponse;
                    if (body != null && body.Applied && !body.IsWaitingForChoice)
                        break;
                }
            }

            return ToHttp(result);
        });
    }

    HttpResponse Cancel(RequestContext ctx)
    {
        var req = ReadMutate(ctx);
        return WithIdempotency(req.ClientRequestId, () =>
        {
            var backend = RequireBackend(out var missing);
            if (missing != null)
                return missing;

            var dryRun = req.DryRun || ctx.DryRun;
            return ToHttp(Run(() => Game.Slots.CancelChoice(backend!, dryRun)));
        });
    }

    ISlotBackend? RequireBackend(out HttpResponse? error)
    {
        var backend = Backend;
        if (backend == null)
        {
            error = Json.Error(409, ErrorCodes.UnsupportedInThisBuild, "slot backend is not initialized");
            return null;
        }

        error = null;
        return backend;
    }

    HttpResponse WithIdempotency(string? clientRequestId, Func<HttpResponse> work)
    {
        if (_idempotency.TryGet(clientRequestId, out var status, out var body))
            return new HttpResponse { Status = status, Body = body };

        HttpResponse response;
        try
        {
            response = work();
        }
        catch (AggregateException ex) when (ex.InnerException is MainThreadTimeoutException)
        {
            response = Json.Error(504, ErrorCodes.MainThreadTimeout, "main thread timed out");
        }
        catch (MainThreadTimeoutException)
        {
            response = Json.Error(504, ErrorCodes.MainThreadTimeout, "main thread timed out");
        }
        catch (Exception ex)
        {
            response = Json.Error(500, ErrorCodes.UnityException, ex.GetBaseException().Message);
        }

        _idempotency.Put(clientRequestId, response.Status, response.Body);
        return response;
    }

    SlotCommandResult Run(Func<SlotCommandResult> work)
    {
        var thread = Thread;
        if (thread == null)
            return work();
        try
        {
            return thread.Run(work).GetAwaiter().GetResult();
        }
        catch (AggregateException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }

    static HttpResponse ToHttp(SlotCommandResult result)
    {
        if (!result.Ok)
            return Json.Error(result.Status, result.Error ?? ErrorCodes.UnityException, result.Message ?? "", result.Phase, result.Generation);
        return Json.Ok(result.Payload ?? new { ok = true });
    }

    static bool TryParseId(RequestContext ctx, out int instanceId, out HttpResponse? error)
    {
        instanceId = 0;
        error = null;
        if (!ctx.RouteValues.TryGetValue("id", out var raw) || !int.TryParse(raw, out instanceId))
        {
            error = Json.Error(404, ErrorCodes.NotFound, "slot id is required");
            return false;
        }

        return true;
    }

    static HarvestRequest ReadHarvest(RequestContext ctx)
    {
        var req = Read<HarvestRequest>(ctx.Body) ?? new HarvestRequest();
        if (ctx.Query.TryGetValue("generation", out var gen) && int.TryParse(gen, out var g))
            req.Generation ??= g;
        return req;
    }

    static SlotMutateRequest ReadMutate(RequestContext ctx)
    {
        var req = Read<SlotMutateRequest>(ctx.Body) ?? new SlotMutateRequest();
        if (ctx.Query.TryGetValue("generation", out var gen) && int.TryParse(gen, out var g))
            req.Generation ??= g;
        return req;
    }

    static T? Read<T>(string body)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            return Json.Deserialize<T>(body);
        }
        catch
        {
            return null;
        }
    }
}
