using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class KingModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("POST", "/king/teleport", Teleport);
        router.Map("POST", "/night/policy", Policy);
    }

    static HttpResponse Teleport(RequestContext ctx) =>
        MainThreadCall.Invoke(() =>
        {
            var illegal = PhaseGate.RejectMutate(
                ctx.Method,
                "/king/teleport",
                Phases.Day,
                Phases.Night,
                Phases.LevelSelect);
            if (illegal != null)
                return illegal;

            var req = PhaseGate.Body<KingTeleportRequest>(ctx);
            var dry = PhaseGate.IsDryRun(ctx, req.DryRun);
            var result = King.Teleport(req.Target, req.Position, dry);
            if (!result.Ok)
            {
                var status = result.Error == ErrorCodes.UnsupportedInThisBuild ? 501 : 400;
                return PhaseGate.Fail(status, result.Error ?? ErrorCodes.NotFound, result.Message ?? "king teleport failed");
            }

            return Json.Ok(result);
        });

    static HttpResponse Policy(RequestContext ctx) =>
        MainThreadCall.Invoke(() =>
        {
            var illegal = PhaseGate.RejectMutate(ctx.Method, "/night/policy", Phases.Day, Phases.Night);
            if (illegal != null)
                return illegal;

            var req = PhaseGate.Body<NightPolicyRequest>(ctx);
            var dry = PhaseGate.IsDryRun(ctx, req.DryRun);
            var result = King.ApplyPolicy(req.Policy, dry);
            if (!result.Ok)
            {
                var status = result.Error == ErrorCodes.UnsupportedInThisBuild ? 501 : 400;
                return PhaseGate.Fail(status, result.Error ?? ErrorCodes.NotFound, result.Message ?? "night policy failed");
            }

            return Json.Ok(result);
        });
}