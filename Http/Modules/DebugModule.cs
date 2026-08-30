using System;
using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http.Modules;

public sealed class DebugModule : IRouteModule
{
    public static Func<HttpResponse>? UpgradeToMax { get; set; }
    public static Func<HttpResponse>? SkipWave { get; set; }
    public static Func<bool, HttpResponse>? Invulnerable { get; set; }
    public static Func<HttpResponse>? Save { get; set; }

    public static void Reset()
    {
        UpgradeToMax = null;
        SkipWave = null;
        Invulnerable = null;
        Save = null;
    }

    public void Register(Router router)
    {
        router.Map("POST", "/debug/upgrade-max", OnUpgradeMax);
        router.Map("POST", "/debug/skip-wave", OnSkipWave);
        router.Map("POST", "/debug/invulnerable", OnInvulnerable);
        router.Map("POST", "/debug/save", OnSave);
    }

    static HttpResponse OnUpgradeMax(RequestContext ctx) =>
        Gated(
            ctx,
            "/debug/upgrade-max",
            PluginConfig.EnableDebugUpgradeToMax,
            Phases.Day,
            UpgradeToMax,
            "upgrade-max");

    static HttpResponse OnSkipWave(RequestContext ctx) =>
        Gated(
            ctx,
            "/debug/skip-wave",
            PluginConfig.EnableDebugCheats,
            Phases.Night,
            SkipWave,
            "skip-wave");

    static HttpResponse OnInvulnerable(RequestContext ctx)
    {
        if (!PluginConfig.EnableDebugCheats)
            return Disabled("/debug/invulnerable");

        return MainThreadCall.Invoke(() =>
        {
            var illegal = PhaseGate.RejectMutate(ctx.Method, "/debug/invulnerable", Phases.Day, Phases.Night);
            if (illegal != null)
                return illegal;
            if (Invulnerable == null)
                return Unattached("invulnerable");
            var enabled = true;
            if (!string.IsNullOrWhiteSpace(ctx.Body))
            {
                var body = Json.Deserialize<InvulnerableRequest>(ctx.Body);
                if (body != null)
                    enabled = body.Enabled;
            }

            return Invulnerable(enabled);
        });
    }

    static HttpResponse OnSave(RequestContext ctx) =>
        Gated(
            ctx,
            "/debug/save",
            PluginConfig.AllowSaveApi,
            null,
            Save,
            "save",
            Phases.Day,
            Phases.Night);

    static HttpResponse Gated(
        RequestContext ctx,
        string path,
        bool enabled,
        string? singlePhase,
        Func<HttpResponse>? hook,
        string action,
        params string[] extraPhases)
    {
        if (!enabled)
            return Disabled(path);

        return MainThreadCall.Invoke(() =>
        {
            string[] allowed;
            if (extraPhases.Length > 0)
                allowed = extraPhases;
            else if (singlePhase != null)
                allowed = new[] { singlePhase };
            else
                allowed = new[] { Phases.Day, Phases.Night };

            var illegal = PhaseGate.RejectMutate(ctx.Method, path, allowed);
            if (illegal != null)
                return illegal;
            if (hook == null)
                return Unattached(action);
            return hook();
        });
    }

    static HttpResponse Disabled(string path) =>
        PhaseGate.Fail(403, ErrorCodes.CheatDisabled, $"{path} is disabled");

    static HttpResponse Unattached(string action) =>
        PhaseGate.Fail(
            501,
            ErrorCodes.UnsupportedInThisBuild,
            $"debug {action} runtime is not attached");

    sealed class InvulnerableRequest
    {
        public bool Enabled { get; set; } = true;
    }
}