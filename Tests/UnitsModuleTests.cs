using ThronefallControl.Dto;
using ThronefallControl.Http;
using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

[Collection("Units")]
public sealed class UnitsModuleTests
{
    [Fact]
    public void Deploy_route_warps_mixed_picks()
    {
        using var scope = new UnitTestScope();
        scope.World.AddUnit(1, "P Knight");
        scope.World.AddUnit(2, "P Knight");
        scope.World.AddUnit(3, "P Knight");
        scope.World.AddUnit(11, "P Crossbows");
        scope.World.AddUnit(12, "P Crossbows");
        scope.World.AddUnit(13, "P Crossbows");
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/units/deploy",
            body: """{"picks":[{"typeName":"P Crossbows","count":3},{"typeName":"P Knight","count":3}],"target":{"x":-24,"y":3,"z":-43},"hold":true,"spacing":2}"""));

        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<UnitsCommandResponse>(res.Body);
        Assert.NotNull(body);
        Assert.Equal("warp", body!.Path);
        Assert.Equal(6, body.Applied.Count);
        Assert.Equal(-43f, scope.World.Units[0].HomePosition.Z);
        Assert.True(scope.World.Units[0].Snapped);
        Assert.True(scope.World.Units[0].HoldPosition);
    }

    [Fact]
    public void Command_route_is_registered_and_holds()
    {
        using var scope = new UnitTestScope();
        scope.World.AddUnit(8801, "P Knight");
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/units/command",
            body: """{"clientRequestId":"u-1","selector":{"ids":[8801]},"target":{"x":12,"y":0,"z":-3},"hold":true,"useSolver":false}"""));

        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<UnitsCommandResponse>(res.Body);
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.Equal("fallback", body.Path);
        Assert.Contains(8801, body.Applied);
        Assert.True(scope.World.Units[0].HoldPosition);
        Assert.Equal(12f, scope.World.Units[0].HomePosition.X);
    }

    [Fact]
    public void Hold_follow_groups_and_send_to_spawn_routes_hit_module()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(7, "P Knight");
        scope.World.CastleCenter = new WorldVec(0, 0, 0);
        scope.World.AddSpawn(220, new WorldVec(8f, 0f, 0f), new WorldVec(10f, 0f, 0f));
        var router = Router.CreateDefault();

        var hold = router.Dispatch(RequestContext.Create(
            "POST", "/units/hold", body: """{"selector":{"ids":[7]}}"""));
        Assert.Equal(200, hold.Status);
        Assert.True(knight.HoldPosition);

        var follow = router.Dispatch(RequestContext.Create(
            "POST", "/units/follow", body: """{"selector":{"typeName":"P Knight"}}"""));
        Assert.Equal(200, follow.Status);
        Assert.True(knight.FollowingPlayer);

        var groups = router.Dispatch(RequestContext.Create(
            "POST", "/units/groups", body: """{"group":2,"selector":{"ids":[7]}}"""));
        Assert.Equal(200, groups.Status);
        Assert.Equal(2, knight.ControlGroup);
        var groupBody = Json.Deserialize<UnitsCommandResponse>(groups.Body);
        Assert.Equal(2, groupBody!.Group);

        var send = router.Dispatch(RequestContext.Create(
            "POST",
            "/units/send-to-spawn",
            body: """{"clientRequestId":"u-5","typeName":"P Knight","spawnId":220,"hold":true}"""));
        Assert.Equal(200, send.Status);
        Assert.Equal(8f, knight.HomePosition.X, 3);
        Assert.True(knight.HoldPosition);
    }

    [Fact]
    public void Stale_id_maps_to_error_envelope()
    {
        using var scope = new UnitTestScope();
        scope.World.StaleIds.Add(42);
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/units/command",
            body: """{"selector":{"ids":[42]},"target":{"x":1,"y":0,"z":1}}"""));

        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.StaleId, err!.Error);
        Assert.Equal("day", err.Phase);
        Assert.Equal(1, err.Generation);
    }

    [Fact]
    public void Illegal_phase_maps_to_error_envelope()
    {
        using var scope = new UnitTestScope();
        scope.World.Phase = "night";
        scope.World.AddUnit(1, "P Knight");
        scope.World.Phase = "menu";
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST", "/units/follow", body: """{"selector":{"ids":[1]}}"""));

        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
    }

    [Fact]
    public void Dry_run_query_does_not_mutate()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(5, "P Knight");
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/units/command?dryRun=true",
            body: """{"selector":{"ids":[5]},"target":{"x":3,"y":0,"z":3},"hold":true}"""));

        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<UnitsCommandResponse>(res.Body);
        Assert.True(body!.DryRun);
        Assert.Equal("command", body.Would!.Action);
        Assert.False(knight.HoldPosition);
        Assert.Equal(0f, knight.HomePosition.X);
    }

    [Fact]
    public void Missing_units_world_is_unsupported()
    {
        var prev = Units.Current;
        Units.Current = null;
        try
        {
            var router = new Router();
            router.AddModule(new ThronefallControl.Http.Modules.UnitsModule());
            var res = router.Dispatch(RequestContext.Create(
                "POST", "/units/hold", body: """{"selector":{"ids":[1]}}"""));
            Assert.Equal(501, res.Status);
            var err = Json.Deserialize<ErrorResponse>(res.Body);
            Assert.Equal(ErrorCodes.UnsupportedInThisBuild, err!.Error);
        }
        finally
        {
            Units.Current = prev;
        }
    }
}
