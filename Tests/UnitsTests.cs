using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

[Collection("Units")]
public sealed class UnitsTests
{
    [Fact]
    public void Deploy_picks_counts_by_type_and_warps()
    {
        using var scope = new UnitTestScope();
        var k1 = scope.World.AddUnit(1, "P Knight");
        var k2 = scope.World.AddUnit(2, "P Knight");
        var k3 = scope.World.AddUnit(3, "P Knight");
        var b1 = scope.World.AddUnit(11, "P Crossbows");
        var b2 = scope.World.AddUnit(12, "P Crossbows");
        var b3 = scope.World.AddUnit(13, "P Crossbows");
        k3.Position = new WorldVec(99, 0, 99);
        b3.Position = new WorldVec(88, 0, 88);

        var outcome = scope.Service.Deploy(
            new[]
            {
                new UnitPick { TypeName = "P Crossbows", Count = 2 },
                new UnitPick { TypeName = "P Knight", Count = 2 }
            },
            new WorldVec(10f, 1f, -4f),
            hold: true,
            spacing: 2f,
            dryRun: false);

        Assert.True(outcome.Ok);
        Assert.Equal("warp", outcome.Path);
        Assert.Equal(4, outcome.Applied.Count);
        Assert.DoesNotContain(3, outcome.Applied);
        Assert.DoesNotContain(13, outcome.Applied);
        Assert.Equal(99f, k3.Position.X);
        Assert.Equal(88f, b3.Position.X);
        Assert.True(k1.HoldPosition);
        Assert.True(b1.Snapped);
        Assert.InRange(k1.Position.X, 7f, 13f);
        Assert.Equal(-4f, k1.HomePosition.Z);
    }

    [Fact]
    public void Deploy_not_enough_units_is_not_found()
    {
        using var scope = new UnitTestScope();
        scope.World.AddUnit(1, "P Knight");
        var outcome = scope.Service.Deploy(
            new[] { new UnitPick { TypeName = "P Knight", Count = 3 } },
            new WorldVec(1, 0, 1),
            hold: true,
            spacing: 2f,
            dryRun: false);
        Assert.False(outcome.Ok);
        Assert.Equal(404, outcome.Status);
        Assert.Equal(ErrorCodes.NotFound, outcome.Error);
    }

    [Fact]
    public void Command_fallback_sets_home_and_hold()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(8801, "P Knight");
        var target = new WorldVec(12f, 0f, -3f);

        var outcome = scope.Service.Command(Ids(8801), target, hold: true, useSolver: true, dryRun: false);

        Assert.True(outcome.Ok);
        Assert.Equal("fallback", outcome.Path);
        Assert.Equal(0, scope.World.SolverAttempts);
        Assert.Equal(12f, knight.HomePosition.X);
        Assert.Equal(-3f, knight.HomePosition.Z);
        Assert.True(knight.HoldPosition);
        Assert.True(knight.Snapped);
        Assert.False(knight.FollowingPlayer);
        Assert.Contains(8801, outcome.Applied);
    }

    [Fact]
    public void Hold_sets_hold_position_and_clears_follow()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(2, "P Knight");
        knight.SetFollowing(true);
        knight.HoldPosition = false;

        var outcome = scope.Service.Hold(Ids(2), dryRun: false);

        Assert.True(outcome.Ok);
        Assert.True(knight.HoldPosition);
        Assert.False(knight.FollowingPlayer);
    }

    [Fact]
    public void Follow_sets_following_and_clears_hold()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(3, "P Knight");
        knight.HoldPosition = true;

        var outcome = scope.Service.Follow(new UnitSelector { TypeName = "P Knight" }, dryRun: false);

        Assert.True(outcome.Ok);
        Assert.True(knight.FollowingPlayer);
        Assert.False(knight.HoldPosition);
    }

    [Fact]
    public void Stale_ids_are_reported_while_others_apply()
    {
        using var scope = new UnitTestScope();
        var live = scope.World.AddUnit(1, "P Knight");
        scope.World.StaleIds.Add(99);
        var target = new WorldVec(4f, 0f, 5f);

        var outcome = scope.Service.Command(Ids(1, 99), target, hold: true, useSolver: false, dryRun: false);

        Assert.True(outcome.Ok);
        Assert.Equal(4f, live.HomePosition.X);
        Assert.True(live.HoldPosition);
        Assert.Contains(1, outcome.Applied);
        Assert.Contains(99, outcome.StaleIds);
    }

    [Fact]
    public void Deploy_stale_id_fails_without_warping()
    {
        using var scope = new UnitTestScope();
        var live = scope.World.AddUnit(1, "P Knight");
        scope.World.StaleIds.Add(99);

        var outcome = scope.Service.Deploy(
            new[] { new UnitPick { Ids = { 1, 99 } } },
            new WorldVec(5f, 0f, 5f),
            hold: false,
            spacing: 2f,
            dryRun: false);

        Assert.False(outcome.Ok);
        Assert.Equal(ErrorCodes.StaleId, outcome.Error);
        Assert.Equal(409, outcome.Status);
        Assert.Equal(0f, live.Position.X);
        Assert.False(live.Snapped);
    }

    [Fact]
    public void All_stale_ids_fail_with_stale_id()
    {
        using var scope = new UnitTestScope();
        scope.World.AddUnit(1, "P Knight");
        scope.World.StaleIds.Add(1);

        var outcome = scope.Service.Command(Ids(1), new WorldVec(1, 0, 1), hold: true, useSolver: false, dryRun: false);

        Assert.False(outcome.Ok);
        Assert.Equal(ErrorCodes.StaleId, outcome.Error);
        Assert.Equal(409, outcome.Status);
        Assert.Contains(1, outcome.StaleIds);
    }

    [Fact]
    public void Solver_flag_false_uses_fallback_even_when_request_asks_for_solver()
    {
        using var scope = new UnitTestScope();
        PluginConfig.UseCommandUnitsSolver = false;
        var knight = scope.World.AddUnit(8, "P Knight");

        var outcome = scope.Service.Command(Ids(8), new WorldVec(9f, 0f, 1f), hold: true, useSolver: true, dryRun: false);

        Assert.True(outcome.Ok);
        Assert.Equal("fallback", outcome.Path);
        Assert.Equal(0, scope.World.SolverAttempts);
        Assert.Equal(9f, knight.HomePosition.X);
        Assert.True(knight.HoldPosition);
    }

    [Fact]
    public void Solver_runs_only_when_config_enabled()
    {
        using var scope = new UnitTestScope();
        PluginConfig.UseCommandUnitsSolver = true;
        var a = scope.World.AddUnit(1, "P Knight");
        var b = scope.World.AddUnit(2, "P Knight");

        var outcome = scope.Service.Command(Ids(1, 2), new WorldVec(10f, 0f, 0f), hold: true, useSolver: null, dryRun: false);

        Assert.True(outcome.Ok);
        Assert.Equal("solver", outcome.Path);
        Assert.Equal(1, scope.World.SolverAttempts);
        Assert.Equal(10f, a.HomePosition.X);
        Assert.Equal(10.25f, b.HomePosition.X);
        Assert.True(a.HoldPosition);
        Assert.True(b.HoldPosition);
    }

    [Fact]
    public void Groups_1_2_3_assign_and_clear_previous_owners()
    {
        using var scope = new UnitTestScope();
        var a = scope.World.AddUnit(1, "P Knight");
        var b = scope.World.AddUnit(2, "P Knight");
        var c = scope.World.AddUnit(3, "Archer");

        Assert.True(scope.Service.AssignGroup(Ids(1, 2), 1, dryRun: false).Ok);
        Assert.Equal(1, a.ControlGroup);
        Assert.Equal(1, b.ControlGroup);
        Assert.Equal(0, c.ControlGroup);

        Assert.True(scope.Service.AssignGroup(Ids(3), 2, dryRun: false).Ok);
        Assert.Equal(1, a.ControlGroup);
        Assert.Equal(2, c.ControlGroup);

        Assert.True(scope.Service.AssignGroup(Ids(1), 3, dryRun: false).Ok);
        Assert.Equal(3, a.ControlGroup);
        Assert.Equal(1, b.ControlGroup);

        Assert.True(scope.Service.AssignGroup(Ids(2), 1, dryRun: false).Ok);
        Assert.Equal(3, a.ControlGroup);
        Assert.Equal(1, b.ControlGroup);
        Assert.Equal(2, c.ControlGroup);
    }

    [Fact]
    public void Send_to_spawn_uses_spawn_line_rally_toward_castle()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(11, "P Knight");
        scope.World.CastleCenter = new WorldVec(0f, 0f, 0f);
        scope.World.AddSpawn(220, new WorldVec(20f, 0f, 0f), new WorldVec(24f, 0f, 8f));

        var outcome = scope.Service.SendToSpawn(
            new UnitSelector { TypeName = "P Knight" },
            spawnId: 220,
            spawnGeneration: null,
            hold: true,
            useSolver: false,
            dryRun: false);

        Assert.True(outcome.Ok);
        Assert.Equal("fallback", outcome.Path);
        Assert.Equal(20f, knight.HomePosition.X, 3);
        Assert.Equal(0f, knight.HomePosition.Z, 3);
        Assert.True(knight.HoldPosition);
        Assert.True(knight.Snapped);
    }

    [Fact]
    public void Send_to_spawn_pushes_off_walls_along_castle_to_spawn()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(12, "P Knight");
        scope.World.CastleCenter = new WorldVec(0f, 0f, 0f);
        scope.World.AddSpawn(221, new WorldVec(20f, 0f, 0f), new WorldVec(30f, 0f, 0f));
        scope.World.Walls.Add(new WorldVec(20f, 0f, 0f));

        var outcome = scope.Service.SendToSpawn(
            new UnitSelector { TypeName = "P Knight" },
            spawnId: 221,
            spawnGeneration: 1,
            hold: true,
            useSolver: false,
            dryRun: false);

        Assert.True(outcome.Ok);
        Assert.Equal(23f, knight.HomePosition.X, 3);
        Assert.Equal(0f, knight.HomePosition.Z, 3);
    }

    [Fact]
    public void Dry_run_does_not_move_units()
    {
        using var scope = new UnitTestScope();
        var knight = scope.World.AddUnit(4, "P Knight");
        knight.HomePosition = new WorldVec(1f, 0f, 1f);

        var outcome = scope.Service.Command(Ids(4), new WorldVec(8f, 0f, 8f), hold: true, useSolver: false, dryRun: true);

        Assert.True(outcome.Ok);
        Assert.True(outcome.DryRun);
        Assert.Equal(1f, knight.HomePosition.X);
        Assert.False(knight.HoldPosition);
        Assert.Contains(4, outcome.Applied);
    }

    [Fact]
    public void Illegal_phase_is_rejected()
    {
        using var scope = new UnitTestScope();
        scope.World.Phase = "menu";
        scope.World.AddUnit(1, "P Knight");

        var outcome = scope.Service.Command(Ids(1), new WorldVec(1, 0, 1), hold: true, useSolver: false, dryRun: false);

        Assert.False(outcome.Ok);
        Assert.Equal(ErrorCodes.IllegalPhase, outcome.Error);
        Assert.Equal(409, outcome.Status);
    }

    [Fact]
    public void Transition_is_rejected()
    {
        using var scope = new UnitTestScope();
        scope.World.TransitionInProgress = true;
        scope.World.AddUnit(1, "P Knight");

        var outcome = scope.Service.Hold(Ids(1), dryRun: false);

        Assert.False(outcome.Ok);
        Assert.Equal(ErrorCodes.TransitionInProgress, outcome.Error);
    }

    static UnitSelector Ids(params int[] ids)
    {
        var selector = new UnitSelector();
        selector.Ids.AddRange(ids);
        return selector;
    }
}
