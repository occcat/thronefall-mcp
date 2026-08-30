using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class LoadoutModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("POST", "/loadout/select", Select);
        router.Map("POST", "/level/start", Start);
    }

    static HttpResponse Select(RequestContext ctx) =>
        MainThreadCall.Invoke(() =>
        {
            var illegal = PhaseGate.RejectMutate(ctx.Method, "/loadout/select", Phases.LevelSelect);
            if (illegal != null)
                return illegal;

            var req = PhaseGate.Body<LoadoutSelectRequest>(ctx);
            var dry = PhaseGate.IsDryRun(ctx, req.DryRun);
            var result = Loadout.Select(req.Name, req.Kind, dry);
            return result.Ok ? Json.Ok(result) : ToError(result.Error, result.Message);
        });

    static HttpResponse Start(RequestContext ctx) =>
        MainThreadCall.Invoke(() =>
        {
            var illegal = PhaseGate.RejectMutate(ctx.Method, "/level/start", Phases.LevelSelect);
            if (illegal != null)
                return illegal;

            var req = PhaseGate.Body<LevelStartRequest>(ctx);
            var dry = PhaseGate.IsDryRun(ctx, req.DryRun);
            var result = Loadout.StartLevel(req.SceneName, dry);
            return result.Ok ? Json.Ok(result) : ToError(result.Error, result.Message);
        });

    static HttpResponse ToError(string? error, string? message)
    {
        var code = error ?? ErrorCodes.NotFound;
        var status = code == ErrorCodes.UnsupportedInThisBuild ? 501 : 404;
        return PhaseGate.Fail(status, code, message ?? code);
    }
}