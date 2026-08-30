using System;
using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Http;

public static class PhaseGate
{
    public static HttpResponse Fail(int status, string error, string message) =>
        Json.Error(status, error, message, RuntimeState.Phase, RuntimeState.Generation);

    public static HttpResponse? RejectMutate(string method, string path, params string[] allowed)
    {
        if (RuntimeState.Transitioning && PluginConfig.RefuseMutateDuringTransition)
        {
            return Fail(
                409,
                ErrorCodes.TransitionInProgress,
                $"{method} {path} refused while scene transition is running");
        }

        var phase = RuntimeState.Phase ?? "";
        foreach (var allowedPhase in allowed)
        {
            if (string.Equals(phase, allowedPhase, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return Fail(
            409,
            ErrorCodes.IllegalPhase,
            $"{method} {path} is illegal in phase={phase}");
    }

    public static T Body<T>(RequestContext ctx) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(ctx.Body))
            return new T();
        return Json.Deserialize<T>(ctx.Body) ?? new T();
    }

    public static bool IsDryRun(RequestContext ctx, bool bodyFlag) => ctx.DryRun || bodyFlag;
}