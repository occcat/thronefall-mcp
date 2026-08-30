using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class UnitsCollectionFixture
{
}

[CollectionDefinition("Units")]
public sealed class UnitsCollection : ICollectionFixture<UnitsCollectionFixture>
{
}

public sealed class FakeUnit : ICommandableUnit
{
    public int InstanceId { get; set; }
    public string TypeName { get; set; } = "";
    public int ControlGroup { get; set; }
    public WorldVec Position { get; set; }
    public WorldVec HomePosition { get; set; }
    public bool HoldPosition { get; set; }
    public bool FollowingPlayer { get; private set; }
    public bool Flying { get; set; }
    public bool Snapped { get; private set; }
    public object? NativeMovement => this;
    public object? NativeTagged => this;

    public void SnapToNavmesh() => Snapped = true;

    public WorldVec GetNearestGroundPosition(WorldVec pos) => pos;

    public void FollowPlayer(bool follow)
    {
        FollowingPlayer = follow;
        HoldPosition = false;
    }

    public void SetFollowing(bool follow) => FollowingPlayer = follow;
}

public sealed class FakeSpawnLine : ISpawnLine
{
    public int InstanceId { get; set; }
    public string Name { get; set; } = "";
    public List<WorldVec> Points { get; } = new();
    public IReadOnlyList<WorldVec> Polyline => Points;
}

public sealed class FakeUnitWorld : IUnitWorld
{
    public int Generation { get; set; } = 1;
    public string Phase { get; set; } = "day";
    public bool TransitionInProgress { get; set; }
    public bool CanCommandUnits { get; set; } = true;
    public bool CanAssignGroups { get; set; } = true;
    public float WallBackOffset => PluginConfig.WallBackOffset;
    public WorldVec? CastleCenter { get; set; }
    public int SolverAttempts { get; private set; }
    public bool SolverShouldSucceed { get; set; } = true;

    public List<FakeUnit> Units { get; } = new();
    public List<FakeSpawnLine> Spawns { get; } = new();
    public List<WorldVec> Walls { get; } = new();
    public HashSet<int> StaleIds { get; } = new();

    public IReadOnlyList<ICommandableUnit> PlayerUnits => Units;

    public FakeUnit AddUnit(int id, string typeName, int group = 0)
    {
        var unit = new FakeUnit
        {
            InstanceId = id,
            TypeName = typeName,
            ControlGroup = group
        };
        Units.Add(unit);
        return unit;
    }

    public FakeSpawnLine AddSpawn(int id, params WorldVec[] points)
    {
        var spawn = new FakeSpawnLine { InstanceId = id, Name = "SpawnLine " + id };
        spawn.Points.AddRange(points);
        Spawns.Add(spawn);
        return spawn;
    }

    public ICommandableUnit? ResolveUnit(int instanceId, int generation, out string? error)
    {
        error = null;
        if (StaleIds.Contains(instanceId) || generation != Generation)
        {
            error = ErrorCodes.StaleId;
            return null;
        }

        var unit = Units.Find(u => u.InstanceId == instanceId);
        if (unit == null)
        {
            error = ErrorCodes.NotFound;
            return null;
        }

        return unit;
    }

    public ISpawnLine? ResolveSpawn(int instanceId, int generation, out string? error)
    {
        error = null;
        if (generation != Generation)
        {
            error = ErrorCodes.StaleId;
            return null;
        }

        var spawn = Spawns.Find(s => s.InstanceId == instanceId);
        if (spawn == null)
        {
            error = ErrorCodes.NotFound;
            return null;
        }

        return spawn;
    }

    public bool IsWallNear(WorldVec point, float radius)
    {
        var r2 = radius * radius;
        foreach (var wall in Walls)
        {
            if (wall.SqrDistance(point) <= r2)
                return true;
        }

        return false;
    }

    public bool TryPlaceWithSolver(IReadOnlyList<ICommandableUnit> units, WorldVec target, bool hold, out string? error)
    {
        SolverAttempts++;
        error = null;
        if (!SolverShouldSucceed)
        {
            error = "solver failed";
            return false;
        }

        var i = 0;
        foreach (var u in units)
        {
            u.HomePosition = target.Add(new WorldVec(i * 0.25f, 0f, 0f));
            u.SnapToNavmesh();
            u.FollowPlayer(false);
            u.HoldPosition = hold;
            i++;
        }

        return true;
    }

    public bool AssignControlGroup(IReadOnlyList<ICommandableUnit> units, int group, out string? error)
    {
        error = null;
        var selected = new HashSet<int>();
        foreach (var u in units)
        {
            selected.Add(u.InstanceId);
            u.ControlGroup = group;
        }

        foreach (var u in Units)
        {
            if (selected.Contains(u.InstanceId))
                continue;
            if (u.ControlGroup == group)
                u.ControlGroup = 0;
        }

        return true;
    }
}

public sealed class UnitTestScope : IDisposable
{
    readonly Units? _prevUnits;
    readonly GameFacade? _prevFacade;
    readonly MainThread? _prevMain;
    readonly bool _prevSolver;
    readonly float _prevWall;

    public FakeUnitWorld World { get; }
    public Units Service { get; }
    public GameFacade Facade { get; }

    public UnitTestScope()
    {
        _prevUnits = Units.Current;
        _prevFacade = GameFacade.Current;
        _prevMain = MainThread.Current;
        _prevSolver = PluginConfig.UseCommandUnitsSolver;
        _prevWall = PluginConfig.WallBackOffset;
        PluginConfig.UseCommandUnitsSolver = false;
        PluginConfig.WallBackOffset = 3f;
        MainThread.Current = null;
        World = new FakeUnitWorld();
        Facade = new GameFacade(World);
        Service = Facade.Units;
        Units.Current = Service;
        GameFacade.Current = Facade;
    }

    public void Dispose()
    {
        Units.Current = _prevUnits;
        GameFacade.Current = _prevFacade;
        MainThread.Current = _prevMain;
        PluginConfig.UseCommandUnitsSolver = _prevSolver;
        PluginConfig.WallBackOffset = _prevWall;
    }
}
