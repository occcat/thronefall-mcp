using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class KingPolicyTests : IDisposable
{
    public KingPolicyTests()
    {
        RuntimeState.Reset();
        King.Reset();
    }

    public void Dispose()
    {
        RuntimeState.Reset();
        King.Reset();
    }

    [Fact]
    public void Human_policy_does_nothing_to_combat()
    {
        var rec = new RecordingKingActions();
        var result = King.ApplyPolicy(NightPolicies.Human, actions: rec);
        Assert.True(result.Ok);
        Assert.Equal(NightPolicies.Human, result.Policy);
        Assert.Equal(0, rec.CombatMutations);
        Assert.Equal(0, rec.TeleportCastle);
        Assert.Equal(0, rec.HoldCalls);
        Assert.Equal(0, rec.ScriptedPostCalls);
        Assert.Equal(0, rec.InvulnerableCalls);
        Assert.False(result.Applied.TeleportKing);
        Assert.False(result.Applied.ChangeHold);
        Assert.False(result.Applied.CommandUnits);
        Assert.False(result.Applied.Invulnerable);
        Assert.Equal("untouched", result.Applied.Combat);
        Assert.Equal(NightPolicies.Human, King.CurrentPolicy);
    }

    [Fact]
    public void Human_policy_http_does_not_touch_combat()
    {
        RuntimeState.Phase = Phases.Night;
        var rec = new RecordingKingActions();
        King.Actions = rec;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/night/policy",
            body: "{\"clientRequestId\":\"np-1\",\"policy\":\"human\"}"));
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<NightPolicyResult>(res.Body);
        Assert.True(body!.Ok);
        Assert.Equal(NightPolicies.Human, body.Policy);
        Assert.Equal("untouched", body.Applied.Combat);
        Assert.False(body.Applied.TeleportKing);
        Assert.False(body.Applied.ChangeHold);
        Assert.False(body.Applied.CommandUnits);
        Assert.False(body.Applied.Invulnerable);
        Assert.Equal(0, rec.CombatMutations);
    }

    [Fact]
    public void Afk_castle_teleports_king_without_invulnerable()
    {
        var rec = new RecordingKingActions();
        var result = King.ApplyPolicy(NightPolicies.AfkCastle, actions: rec);
        Assert.True(result.Ok);
        Assert.Equal(1, rec.TeleportCastle);
        Assert.Equal(0, rec.InvulnerableCalls);
        Assert.True(result.Applied.TeleportKing);
        Assert.False(result.Applied.Invulnerable);
        Assert.False(result.Applied.ChangeHold);
    }

    [Fact]
    public void Teleport_does_not_make_king_invulnerable()
    {
        RuntimeState.Phase = Phases.Day;
        var rec = new RecordingKingActions();
        King.Actions = rec;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/king/teleport",
            body: "{\"target\":\"castle\"}"));
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<KingTeleportResult>(res.Body);
        Assert.True(body!.Ok);
        Assert.False(body.Invulnerable);
        Assert.Equal(1, rec.TeleportCastle);
        Assert.Equal(0, rec.InvulnerableCalls);
    }

    [Fact]
    public void Teleport_in_menu_is_illegal_phase()
    {
        RuntimeState.Phase = Phases.Menu;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/king/teleport",
            body: "{\"target\":\"start\"}"));
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
    }
}