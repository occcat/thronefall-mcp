using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class SpawnsTests
{
    [Fact]
    public void Rally_picks_polyline_point_closest_to_castle()
    {
        var line = new[] { new WorldVec(20f, 0f, 0f), new WorldVec(24f, 0f, 8f) };
        var rally = Spawns.ComputeRally(line, new WorldVec(0f, 0f, 0f), wallBackOffset: 3f);
        Assert.False(rally.PushedFromWall);
        Assert.Equal(20f, rally.Point.X, 3);
        Assert.Equal(0f, rally.Point.Z, 3);
    }

    [Fact]
    public void Rally_offsets_along_castle_to_spawn_when_wall_is_near()
    {
        var line = new[] { new WorldVec(20f, 0f, 0f), new WorldVec(30f, 0f, 0f) };
        var rally = Spawns.ComputeRally(
            line,
            new WorldVec(0f, 0f, 0f),
            wallBackOffset: 3f,
            isWallNear: (p, _) => p.X <= 20.1f);

        Assert.True(rally.PushedFromWall);
        Assert.Equal(23f, rally.Point.X, 3);
        Assert.Equal(0f, rally.Point.Z, 3);
    }

    [Fact]
    public void Snapshot_style_rally_matches_ComputeRally_for_the_same_spawnLine()
    {
        var castle = new WorldVec(0f, 0f, 0f);
        var tonight = new[] { new WorldVec(20f, 0f, 0f), new WorldVec(24f, 0f, 8f) };
        var extra = new[] { new WorldVec(80f, 0f, 0f), new WorldVec(90f, 0f, 0f) };
        const float offset = 3f;

        var fullMap = NextWave.IndexRallies(
            new (int, IReadOnlyList<WorldVec>)[] { (1, tonight), (2, extra) },
            castle,
            offset);
        var compute = Spawns.ComputeRally(tonight, castle, offset).Point.ToDto();

        Assert.Equal(compute.X, fullMap[1].X, 3);
        Assert.Equal(compute.Y, fullMap[1].Y, 3);
        Assert.Equal(compute.Z, fullMap[1].Z, 3);
        Assert.Equal(20f, fullMap[1].X, 3);
    }
}
