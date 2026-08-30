using System;
using System.Collections.Generic;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class UnitsModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("POST", "/units/command", ctx => Handle(ctx, Command));
        router.Map("POST", "/units/hold", ctx => Handle(ctx, Hold));
        router.Map("POST", "/units/follow", ctx => Handle(ctx, Follow));
        router.Map("POST", "/units/groups", ctx => Handle(ctx, Groups));
        router.Map("POST", "/units/send-to-spawn", ctx => Handle(ctx, SendToSpawn));
        router.Map("POST", "/units/deploy", ctx => Handle(ctx, Deploy));
    }

    static HttpResponse Handle(RequestContext ctx, Func<RequestContext, UnitCommandOutcome> work)
    {
        var mt = MainThread.Current;
        if (mt == null)
            return ToHttp(Safe(ctx, work));

        try
        {
            return ToHttp(mt.Run(() => Safe(ctx, work)).GetAwaiter().GetResult());
        }
        catch (AggregateException ex) when (ex.InnerException is MainThreadTimeoutException)
        {
            return Json.Error(504, ErrorCodes.MainThreadTimeout, "main_thread_timeout");
        }
        catch (MainThreadTimeoutException)
        {
            return Json.Error(504, ErrorCodes.MainThreadTimeout, "main_thread_timeout");
        }
        catch (Exception ex)
        {
            var inner = ex is AggregateException ag ? ag.InnerException ?? ex : ex;
            return Json.Error(500, ErrorCodes.UnityException, inner.Message);
        }
    }

    static UnitCommandOutcome Safe(RequestContext ctx, Func<RequestContext, UnitCommandOutcome> work)
    {
        try
        {
            return work(ctx);
        }
        catch (Exception ex)
        {
            return new UnitCommandOutcome
            {
                Ok = false,
                Status = 500,
                Error = ErrorCodes.UnityException,
                Message = ex.Message
            };
        }
    }

    static UnitCommandOutcome Command(RequestContext ctx)
    {
        var units = RequireUnits();
        if (units.Error != null)
            return units.Error;

        var req = Read<UnitsCommandRequest>(ctx);
        if (req.Target == null)
            return Bad(400, ErrorCodes.NotFound, "target is required");

        return units.Value!.Command(
            ToSelector(req.Selector),
            WorldVec.FromDto(req.Target),
            req.Hold,
            req.UseSolver,
            ctx.DryRun || req.DryRun);
    }

    static UnitCommandOutcome Hold(RequestContext ctx)
    {
        var units = RequireUnits();
        if (units.Error != null)
            return units.Error;
        var req = Read<UnitsHoldRequest>(ctx);
        return units.Value!.Hold(ToSelector(req.Selector), ctx.DryRun || req.DryRun);
    }

    static UnitCommandOutcome Follow(RequestContext ctx)
    {
        var units = RequireUnits();
        if (units.Error != null)
            return units.Error;
        var req = Read<UnitsFollowRequest>(ctx);
        return units.Value!.Follow(ToSelector(req.Selector), ctx.DryRun || req.DryRun);
    }

    static UnitCommandOutcome Groups(RequestContext ctx)
    {
        var units = RequireUnits();
        if (units.Error != null)
            return units.Error;
        var req = Read<UnitsGroupRequest>(ctx);
        return units.Value!.AssignGroup(ToSelector(req.Selector), req.Group, ctx.DryRun || req.DryRun);
    }

    static UnitCommandOutcome SendToSpawn(RequestContext ctx)
    {
        var units = RequireUnits();
        if (units.Error != null)
            return units.Error;
        var req = Read<UnitsSendToSpawnRequest>(ctx);
        var selector = ToSelector(req.Selector);
        if (string.IsNullOrEmpty(selector.TypeName) && !string.IsNullOrEmpty(req.TypeName))
            selector.TypeName = req.TypeName;

        var spawnId = req.Spawn?.InstanceId ?? req.SpawnId;
        int? spawnGen = req.Spawn != null && req.Spawn.Generation != 0 ? req.Spawn.Generation : null;
        if (spawnId == 0)
            return Bad(400, ErrorCodes.NotFound, "spawnId is required");

        return units.Value!.SendToSpawn(
            selector,
            spawnId,
            spawnGen,
            req.Hold,
            req.UseSolver,
            ctx.DryRun || req.DryRun);
    }

    static UnitCommandOutcome Deploy(RequestContext ctx)
    {
        var units = RequireUnits();
        if (units.Error != null)
            return units.Error;

        var req = Read<UnitsDeployRequest>(ctx);
        if (req.Target == null)
            return Bad(400, ErrorCodes.NotFound, "target is required");

        var picks = new List<UnitPick>();
        foreach (var dto in req.Picks ?? new List<UnitPickDto>())
        {
            var pick = new UnitPick
            {
                TypeName = dto.TypeName,
                Count = dto.Count
            };
            if (dto.Ids != null)
                pick.Ids.AddRange(dto.Ids);
            picks.Add(pick);
        }

        return units.Value!.Deploy(
            picks,
            WorldVec.FromDto(req.Target),
            req.Hold,
            req.Spacing,
            ctx.DryRun || req.DryRun);
    }

    static (Units? Value, UnitCommandOutcome? Error) RequireUnits()
    {
        var units = ThronefallControl.Game.Units.Current;
        if (units == null)
        {
            return (null, new UnitCommandOutcome
            {
                Ok = false,
                Status = 501,
                Error = ErrorCodes.UnsupportedInThisBuild,
                Message = "unit world not initialized"
            });
        }

        return (units, null);
    }

    static T Read<T>(RequestContext ctx) where T : new()
    {
        if (string.IsNullOrWhiteSpace(ctx.Body))
            return new T();
        try
        {
            return Json.Deserialize<T>(ctx.Body) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    static UnitSelector ToSelector(UnitSelectorDto? dto)
    {
        var selector = new UnitSelector
        {
            TypeName = dto?.TypeName,
            Group = dto?.Group
        };
        if (dto?.Ids != null)
            selector.Ids.AddRange(dto.Ids);
        return selector;
    }

    static UnitCommandOutcome Bad(int status, string error, string message) =>
        new()
        {
            Ok = false,
            Status = status,
            Error = error,
            Message = message
        };

    static HttpResponse ToHttp(UnitCommandOutcome outcome)
    {
        var phase = ThronefallControl.Game.Units.Current?.World.Phase;
        var generation = ThronefallControl.Game.Units.Current?.World.Generation;
        if (!outcome.Ok)
        {
            return Json.Error(
                outcome.Status <= 0 ? 500 : outcome.Status,
                outcome.Error ?? ErrorCodes.UnityException,
                outcome.Message ?? "unit command failed",
                phase,
                generation);
        }

        var body = new UnitsCommandResponse
        {
            Ok = true,
            DryRun = outcome.DryRun,
            Path = outcome.Path,
            Applied = outcome.Applied,
            StaleIds = outcome.StaleIds,
            NotFound = outcome.NotFound,
            Target = outcome.Target?.ToDto(),
            Group = outcome.Group
        };
        if (outcome.DryRun)
        {
            body.Would = new DryRunWouldDto
            {
                Action = outcome.Action,
                Blocked = false
            };
        }

        return Json.Ok(body);
    }
}
