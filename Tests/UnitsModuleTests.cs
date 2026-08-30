using ThronefallControl.Dto;
using ThronefallControl.Http;
using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

[Collection("Units")]
public sealed class UnitsModuleTests
{
    [Fact]
    public void Deploy_route_returns_200()
    {
        using var scope = new UnitTestScope();
        scope.World.AddUnit(1, "P Knight");
        var res = Router.CreateDefault().Dispatch(RequestContext.Create(
            "POST",
            "/units/deploy",
            body: """{"picks":[{"typeName":"P Knight","count":1}],"target":{"x":-24,"y":3,"z":-43},"hold":true}"""));

        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<UnitsCommandResponse>(res.Body);
        Assert.True(body!.Ok);
        Assert.Equal("warp", body.Path);
    }

    [Fact]
    public void Stale_id_maps_to_error_envelope()
    {
        using var scope = new UnitTestScope();
        scope.World.StaleIds.Add(42);
        var res = Router.CreateDefault().Dispatch(RequestContext.Create(
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
        scope.World.AddUnit(1, "P Knight");
        scope.World.Phase = "menu";
        var res = Router.CreateDefault().Dispatch(RequestContext.Create(
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
        var res = Router.CreateDefault().Dispatch(RequestContext.Create(
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
