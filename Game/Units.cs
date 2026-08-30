using System;
using System.Collections;
using System.Collections.Generic;
using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public interface ICommandableUnit
{
    int InstanceId { get; }
    string TypeName { get; }
    int ControlGroup { get; set; }
    WorldVec Position { get; }
    WorldVec HomePosition { get; set; }
    bool HoldPosition { get; set; }
    bool FollowingPlayer { get; }
    bool Flying { get; }
    object? NativeMovement { get; }
    object? NativeTagged { get; }
    void SnapToNavmesh();
    WorldVec GetNearestGroundPosition(WorldVec pos);
    void FollowPlayer(bool follow);
}

public interface ISpawnLine
{
    int InstanceId { get; }
    string Name { get; }
    IReadOnlyList<WorldVec> Polyline { get; }
}

public interface IUnitWorld
{
    int Generation { get; }
    string Phase { get; }
    bool TransitionInProgress { get; }
    bool CanCommandUnits { get; }
    bool CanAssignGroups { get; }
    float WallBackOffset { get; }
    WorldVec? CastleCenter { get; }
    IReadOnlyList<ICommandableUnit> PlayerUnits { get; }
    int SolverAttempts { get; }
    ICommandableUnit? ResolveUnit(int instanceId, int generation, out string? error);
    ISpawnLine? ResolveSpawn(int instanceId, int generation, out string? error);
    bool IsWallNear(WorldVec point, float radius);
    bool TryPlaceWithSolver(IReadOnlyList<ICommandableUnit> units, WorldVec target, bool hold, out string? error);
    bool AssignControlGroup(IReadOnlyList<ICommandableUnit> units, int group, out string? error);
}

public sealed class UnitSelector
{
    public List<int> Ids { get; } = new();
    public string? TypeName { get; set; }
    public int? Group { get; set; }
}

public sealed class UnitCommandOutcome
{
    public bool Ok { get; set; }
    public int Status { get; set; } = 200;
    public string? Error { get; set; }
    public string? Message { get; set; }
    public string Path { get; set; } = "fallback";
    public bool DryRun { get; set; }
    public string Action { get; set; } = "";
    public List<int> Applied { get; } = new();
    public List<int> StaleIds { get; } = new();
    public List<int> NotFound { get; } = new();
    public WorldVec? Target { get; set; }
    public int Group { get; set; }
}

public sealed class Units
{
    public static Units? Current { get; set; }

    public IUnitWorld World { get; }

    public Units(IUnitWorld world) => World = world ?? throw new ArgumentNullException(nameof(world));

    public static List<UnitDto> Snapshot(IdRegistry ids)
    {
        try
        {
            return Observation.ReadUnits(ids) ?? new List<UnitDto>();
        }
        catch
        {
            return new List<UnitDto>();
        }
    }

    public static EnemySummaryDto SnapshotEnemies(IdRegistry ids)
    {
        try
        {
            return Observation.ReadEnemies(ids) ?? new EnemySummaryDto();
        }
        catch
        {
            return new EnemySummaryDto();
        }
    }

    public UnitCommandOutcome Command(UnitSelector selector, WorldVec target, bool hold, bool? useSolver, bool dryRun) =>
        Mutate("command", selector, dryRun, (units, outcome) =>
        {
            outcome.Target = target;
            var path = "fallback";
            if (ShouldUseSolver(useSolver))
            {
                if (World.TryPlaceWithSolver(units, target, hold, out _))
                    path = "solver";
                else
                    ApplyFallback(units, target, hold);
            }
            else
            {
                ApplyFallback(units, target, hold);
            }

            outcome.Path = path;
        });

    public UnitCommandOutcome Hold(UnitSelector selector, bool dryRun) =>
        Mutate("hold", selector, dryRun, (units, _) =>
        {
            foreach (var u in units)
            {
                u.FollowPlayer(false);
                u.HoldPosition = true;
            }
        });

    public UnitCommandOutcome Follow(UnitSelector selector, bool dryRun) =>
        Mutate("follow", selector, dryRun, (units, _) =>
        {
            foreach (var u in units)
                u.FollowPlayer(true);
        });

    public UnitCommandOutcome AssignGroup(UnitSelector selector, int group, bool dryRun)
    {
        if (group is < 1 or > 3)
        {
            return Fail(400, ErrorCodes.NotFound, "group must be 1, 2, or 3");
        }

        var gate = Gate("groups");
        if (gate != null)
            return gate;
        if (!World.CanAssignGroups)
        {
            return Fail(501, ErrorCodes.UnsupportedInThisBuild, "control groups unavailable in this build");
        }

        return Mutate("groups", selector, dryRun, (units, outcome) =>
        {
            outcome.Group = group;
            if (!World.AssignControlGroup(units, group, out var error))
            {
                outcome.Ok = false;
                outcome.Status = 501;
                outcome.Error = ErrorCodes.UnsupportedInThisBuild;
                outcome.Message = error ?? "failed to assign control group";
            }
        });
    }

    public UnitCommandOutcome SendToSpawn(UnitSelector selector, int spawnId, int? spawnGeneration, bool hold, bool? useSolver, bool dryRun)
    {
        var gate = Gate("send-to-spawn");
        if (gate != null)
            return gate;

        var gen = spawnGeneration ?? World.Generation;
        var spawn = World.ResolveSpawn(spawnId, gen, out var error);
        if (spawn == null)
        {
            var code = error ?? ErrorCodes.NotFound;
            return Fail(code == ErrorCodes.StaleId ? 409 : 404, code,
                code == ErrorCodes.StaleId ? "stale spawn id" : "spawn not found");
        }

        var rally = Spawns.ComputeRally(
            spawn.Polyline,
            World.CastleCenter,
            World.WallBackOffset,
            World.IsWallNear);

        if (string.IsNullOrWhiteSpace(selector.TypeName) && selector.Ids.Count == 0 && selector.Group == null)
            selector.TypeName = null;

        return Command(selector, rally.Point, hold, useSolver, dryRun);
    }

    static bool ShouldUseSolver(bool? request) =>
        request != false && PluginConfig.UseCommandUnitsSolver;

    static void ApplyFallback(IReadOnlyList<ICommandableUnit> units, WorldVec target, bool hold)
    {
        foreach (var u in units)
        {
            var snapped = u.GetNearestGroundPosition(target);
            u.HomePosition = snapped;
            u.SnapToNavmesh();
            u.FollowPlayer(false);
            u.HoldPosition = hold;
        }
    }

    UnitCommandOutcome Mutate(
        string action,
        UnitSelector selector,
        bool dryRun,
        Action<IReadOnlyList<ICommandableUnit>, UnitCommandOutcome> apply)
    {
        var gate = Gate(action);
        if (gate != null)
            return gate;
        if (!World.CanCommandUnits && action is "command" or "hold" or "follow" or "send-to-spawn")
        {
            return Fail(501, ErrorCodes.UnsupportedInThisBuild, "unit command members missing in this build");
        }

        var picked = Select(selector);
        if (picked.Units.Count == 0)
        {
            if (picked.StaleIds.Count > 0)
            {
                var stale = Fail(409, ErrorCodes.StaleId, "stale unit id");
                stale.StaleIds.AddRange(picked.StaleIds);
                stale.NotFound.AddRange(picked.NotFound);
                return stale;
            }

            var missing = Fail(404, ErrorCodes.NotFound, "no units matched selector");
            missing.NotFound.AddRange(picked.NotFound);
            return missing;
        }

        var outcome = new UnitCommandOutcome
        {
            Ok = true,
            Status = 200,
            Action = action,
            DryRun = dryRun,
            Path = "fallback"
        };
        foreach (var u in picked.Units)
            outcome.Applied.Add(u.InstanceId);
        outcome.StaleIds.AddRange(picked.StaleIds);
        outcome.NotFound.AddRange(picked.NotFound);

        if (dryRun)
            return outcome;

        apply(picked.Units, outcome);
        return outcome;
    }

    UnitCommandOutcome? Gate(string action)
    {
        if (World.TransitionInProgress)
        {
            return Fail(409, ErrorCodes.TransitionInProgress,
                $"POST /units/{action} refused during scene transition");
        }

        var phase = (World.Phase ?? "").Trim().ToLowerInvariant();
        if (phase is not "day" and not "night")
        {
            return Fail(409, ErrorCodes.IllegalPhase,
                $"POST /units/{action} is illegal in phase={World.Phase}");
        }

        return null;
    }

    sealed class Selection
    {
        public List<ICommandableUnit> Units { get; } = new();
        public List<int> StaleIds { get; } = new();
        public List<int> NotFound { get; } = new();
    }

    Selection Select(UnitSelector selector)
    {
        var result = new Selection();
        selector ??= new UnitSelector();
        if (selector.Ids.Count > 0)
        {
            var seen = new HashSet<int>();
            foreach (var id in selector.Ids)
            {
                if (!seen.Add(id))
                    continue;
                var unit = World.ResolveUnit(id, World.Generation, out var error);
                if (error == ErrorCodes.StaleId)
                    result.StaleIds.Add(id);
                else if (unit == null)
                    result.NotFound.Add(id);
                else
                    result.Units.Add(unit);
            }

            return result;
        }

        foreach (var unit in World.PlayerUnits)
        {
            if (!string.IsNullOrEmpty(selector.TypeName) && !TypeMatches(unit.TypeName, selector.TypeName))
                continue;
            if (selector.Group is int g && unit.ControlGroup != g)
                continue;
            if (string.IsNullOrEmpty(selector.TypeName) && selector.Group == null)
                continue;
            result.Units.Add(unit);
        }

        return result;
    }

    static bool TypeMatches(string name, string typeName)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(typeName))
            return false;
        var trimmed = name.Replace("(Clone)", "").Trim();
        return trimmed.Equals(typeName, StringComparison.OrdinalIgnoreCase)
               || name.Equals(typeName, StringComparison.OrdinalIgnoreCase);
    }

    UnitCommandOutcome Fail(int status, string error, string message) =>
        new()
        {
            Ok = false,
            Status = status,
            Error = error,
            Message = message
        };
}

public sealed class LiveUnitWorld : IUnitWorld
{
    readonly GameFacade? _facade;
    int _solverAttempts;

    public LiveUnitWorld(GameFacade? facade = null) => _facade = facade;

    public int Generation => _facade?.Ids.SceneGeneration ?? 0;
    public float WallBackOffset => PluginConfig.WallBackOffset;
    public int SolverAttempts => _solverAttempts;
    public bool CanCommandUnits => ReflectionCache.PathfindReady;
    public bool CanAssignGroups => ReflectionCache.GroupsReady;

    public bool TransitionInProgress
    {
        get
        {
            try
            {
                var stm = ReflectionCache.GetSceneTransition();
                if (stm == null || ReflectionCache.SceneTransitionIsRunning == null)
                    return false;
                var v = ReflectionCache.SceneTransitionIsRunning.GetValue(stm, null);
                return v is true || v is 1;
            }
            catch
            {
                return false;
            }
        }
    }

    public string Phase
    {
        get
        {
            try
            {
                if (TransitionInProgress)
                    return "transition";

                var stm = ReflectionCache.GetSceneTransition();
                if (stm != null && ReflectionCache.CurrentSceneState != null)
                {
                    var state = ReflectionCache.CurrentSceneState.GetValue(stm, null);
                    var name = state?.ToString() ?? "";
                    if (name.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "menu";
                    if (name.IndexOf("LevelSelect", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "level_select";
                }

                var dnc = ReflectionCache.GetDayNight();
                if (dnc != null && ReflectionCache.CurrentTimestate != null)
                {
                    var ts = ReflectionCache.CurrentTimestate.GetValue(dnc, null)?.ToString() ?? "";
                    if (ts.Equals("Night", StringComparison.OrdinalIgnoreCase))
                        return "night";
                    if (ts.Equals("Day", StringComparison.OrdinalIgnoreCase))
                        return "day";
                }

                return ReflectionCache.GetTagManager() != null ? "day" : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }

    public WorldVec? CastleCenter
    {
        get
        {
            var tagged = FindClosest(new WorldVec(0, 0, 0), ReflectionCache.ETagCastleCenter);
            return tagged == null ? null : TransformPosition(tagged);
        }
    }

    public IReadOnlyList<ICommandableUnit> PlayerUnits
    {
        get
        {
            var list = new List<ICommandableUnit>();
            foreach (var tagged in EnumeratePlayerTagged())
            {
                var unit = WrapTagged(tagged);
                if (unit != null)
                    list.Add(unit);
            }

            return list;
        }
    }

    public ICommandableUnit? ResolveUnit(int instanceId, int generation, out string? error)
    {
        error = null;
        if (generation != Generation)
        {
            error = ErrorCodes.StaleId;
            return null;
        }

        foreach (var u in PlayerUnits)
        {
            if (u.InstanceId == instanceId)
                return u;
        }

        error = ErrorCodes.NotFound;
        return null;
    }

    public ISpawnLine? ResolveSpawn(int instanceId, int generation, out string? error)
    {
        error = null;
        if (generation != Generation)
        {
            error = ErrorCodes.StaleId;
            return null;
        }

        foreach (var line in FindSpawnLines())
        {
            if (InstanceIdOf(line) == instanceId)
                return new LiveSpawnLine(line, instanceId);
        }

        error = ErrorCodes.NotFound;
        return null;
    }

    public bool IsWallNear(WorldVec point, float radius)
    {
        try
        {
            var tm = ReflectionCache.GetTagManager();
            if (tm == null || ReflectionCache.FindUnsortedTaggedObjectsInRange == null)
            {
                var closest = FindClosest(point, ReflectionCache.ETagWall);
                if (closest == null)
                    return false;
                var pos = TransformPosition(closest);
                return pos.SqrDistance(point) <= radius * radius;
            }

            var must = ReflectionCache.NewETagList(ReflectionCache.ETagWall);
            var mayNot = ReflectionCache.NewETagList();
            var found = ReflectionCache.FindUnsortedTaggedObjectsInRange.Invoke(
                tm, new[] { ReflectionCache.ToVector3(point), must, mayNot, radius });
            if (found == null)
                return false;
            if (found is ICollection col)
                return col.Count > 0;
            foreach (var _ in Enumerate(found))
                return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool TryPlaceWithSolver(IReadOnlyList<ICommandableUnit> units, WorldVec target, bool hold, out string? error)
    {
        error = null;
        _solverAttempts++;
        if (!PluginConfig.UseCommandUnitsSolver || !ReflectionCache.CommandUnitsReady)
        {
            error = "solver disabled";
            return false;
        }

        var cu = ReflectionCache.GetInstance();
        if (cu == null)
        {
            error = "CommandUnits.instance missing";
            return false;
        }

        try
        {
            SetTransformPosition(cu, target);
            FillCommanding(cu, units);
            if (ReflectionCache.Commanding != null)
                ReflectionCache.Commanding.SetValue(cu, true);

            ReflectionCache.PlaceCommandedUnits!.Invoke(cu, new object[] { false });

            if (ReflectionCache.Commanding != null)
                ReflectionCache.Commanding.SetValue(cu, false);

            if (hold && ReflectionCache.MakeUnitsInBufferHoldPosition != null)
                ReflectionCache.MakeUnitsInBufferHoldPosition.Invoke(cu, Array.Empty<object>());

            return true;
        }
        catch (Exception ex)
        {
            error = ex.InnerException?.Message ?? ex.Message;
            try
            {
                if (ReflectionCache.Commanding != null)
                    ReflectionCache.Commanding.SetValue(cu, false);
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }

    public bool AssignControlGroup(IReadOnlyList<ICommandableUnit> units, int group, out string? error)
    {
        error = null;
        var tag = ReflectionCache.GroupToETag(group);
        if (tag < 0)
        {
            error = "invalid group";
            return false;
        }

        if (!ReflectionCache.GroupsReady)
        {
            error = "AddUnitGroupTag missing";
            return false;
        }

        try
        {
            var selected = new HashSet<int>();
            foreach (var u in units)
            {
                selected.Add(u.InstanceId);
                u.ControlGroup = group;
            }

            foreach (var u in PlayerUnits)
            {
                if (selected.Contains(u.InstanceId))
                    continue;
                if (u.ControlGroup == group)
                    u.ControlGroup = 0;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    static void FillCommanding(object cu, IReadOnlyList<ICommandableUnit> units)
    {
        var list = ReflectionCache.PlayerUnitsCommanding?.GetValue(cu);
        if (list != null)
            TryClear(list);

        var buffer = ReflectionCache.PlayerUnitsCommandingBuffer?.GetValue(cu);
        if (buffer != null)
            TryClear(buffer);

        foreach (var u in units)
        {
            if (u.NativeTagged != null && ReflectionCache.OnUnitAdd != null)
            {
                ReflectionCache.OnUnitAdd.Invoke(cu, new[] { u.NativeTagged, false });
                continue;
            }

            if (list != null && u.NativeMovement != null)
                TryAdd(list, u.NativeMovement);
        }
    }

    static void TryClear(object list)
    {
        var m = list.GetType().GetMethod("Clear", Type.EmptyTypes);
        m?.Invoke(list, null);
    }

    static void TryAdd(object list, object item)
    {
        var m = list.GetType().GetMethod("Add");
        m?.Invoke(list, new[] { item });
    }

    IEnumerable<object> EnumeratePlayerTagged()
    {
        var tm = ReflectionCache.GetTagManager();
        if (tm == null || ReflectionCache.PlayerUnits == null)
            yield break;
        object? list;
        try
        {
            list = ReflectionCache.PlayerUnits.GetValue(tm, null);
        }
        catch
        {
            yield break;
        }

        foreach (var item in Enumerate(list))
            yield return item;
    }

    IEnumerable<object> FindSpawnLines()
    {
        var t = ReflectionCache.EnemySpawnLineType;
        if (t == null)
            yield break;
        var found = FindObjectsOfType(t);
        foreach (var item in found)
            yield return item;
    }

    object? FindClosest(WorldVec from, int tag)
    {
        var tm = ReflectionCache.GetTagManager();
        if (tm == null || ReflectionCache.FindClosestTaggedObjectWithTags == null)
            return null;
        try
        {
            var must = ReflectionCache.NewETagList(tag);
            var mayNot = ReflectionCache.NewETagList();
            return ReflectionCache.FindClosestTaggedObjectWithTags.Invoke(
                tm, new[] { ReflectionCache.ToVector3(from), must, mayNot });
        }
        catch
        {
            return null;
        }
    }

    static IEnumerable<object> FindObjectsOfType(Type type)
    {
        var objType = ReflectionCache.UnityObjectType;
        if (objType == null)
            yield break;
        object? result = null;
        try
        {
            var m = objType.GetMethod("FindObjectsOfType", new[] { typeof(Type) });
            result = m?.Invoke(null, new object[] { type });
        }
        catch
        {
            result = null;
        }

        if (result == null)
        {
            try
            {
                foreach (var m in objType.GetMethods())
                {
                    if (m.Name != "FindObjectsByType" || m.GetParameters().Length < 1)
                        continue;
                    var ps = m.GetParameters();
                    if (ps[0].ParameterType != typeof(Type))
                        continue;
                    var args = new object?[ps.Length];
                    args[0] = type;
                    for (var i = 1; i < ps.Length; i++)
                    {
                        var pt = ps[i].ParameterType;
                        if (pt.IsEnum)
                            args[i] = Enum.ToObject(pt, 0);
                        else if (pt.IsValueType)
                            args[i] = Activator.CreateInstance(pt);
                    }

                    result = m.Invoke(null, args);
                    break;
                }
            }
            catch
            {
                result = null;
            }
        }

        foreach (var item in Enumerate(result))
            yield return item;
    }

    static IEnumerable<object> Enumerate(object? list)
    {
        if (list is IEnumerable e)
        {
            foreach (var item in e)
            {
                if (item != null)
                    yield return item;
            }
        }
    }

    LiveUnit? WrapTagged(object tagged)
    {
        var movement = GetComponent(tagged, ReflectionCache.PathfindMovementPlayerunitType);
        if (movement == null)
            return null;
        var id = InstanceIdOf(tagged);
        Register(id, "unit", GameObjectName(tagged), movement);
        return new LiveUnit(id, movement, tagged);
    }

    void Register(int id, string kind, string name, object target)
    {
        try
        {
            _facade?.Ids.Register(id, kind, name, target);
        }
        catch
        {
            // registry is best-effort during command
        }
    }

    static int InstanceIdOf(object obj)
    {
        var go = ReflectionCache.GetMember(obj, "gameObject") ?? obj;
        var v = ReflectionCache.CallNamed(go, "GetInstanceID");
        return v is int i ? i : 0;
    }

    static string GameObjectName(object obj)
    {
        var go = ReflectionCache.GetMember(obj, "gameObject") ?? obj;
        return ReflectionCache.GetMember(go, "name") as string ?? "";
    }

    static WorldVec TransformPosition(object obj)
    {
        var tr = ReflectionCache.GetMember(obj, "transform") ?? obj;
        return ReflectionCache.FromVector3(ReflectionCache.GetMember(tr, "position"));
    }

    static void SetTransformPosition(object obj, WorldVec pos)
    {
        var tr = ReflectionCache.GetMember(obj, "transform");
        if (tr == null)
            return;
        var v3 = ReflectionCache.ToVector3(pos);
        ReflectionCache.SetMember(tr, "position", v3);
    }

    static object? GetComponent(object component, Type? type)
    {
        if (type == null)
            return null;
        var result = ReflectionCache.CallNamed(component, "GetComponent", type);
        if (result != null)
            return result;
        var go = ReflectionCache.GetMember(component, "gameObject");
        return go == null ? null : ReflectionCache.CallNamed(go, "GetComponent", type);
    }

    sealed class LiveUnit : ICommandableUnit
    {
        readonly object _movement;
        readonly object? _tagged;

        public LiveUnit(int instanceId, object movement, object? tagged)
        {
            InstanceId = instanceId;
            _movement = movement;
            _tagged = tagged;
        }

        public int InstanceId { get; }
        public object? NativeMovement => _movement;
        public object? NativeTagged => _tagged;

        public string TypeName => GameObjectName(_tagged ?? _movement);

        public WorldVec Position => TransformPosition(_movement);

        public WorldVec HomePosition
        {
            get => ReflectionCache.FromVector3(ReflectionCache.HomePosition?.GetValue(_movement, null));
            set => ReflectionCache.HomePosition?.SetValue(_movement, ReflectionCache.ToVector3(value), null);
        }

        public bool HoldPosition
        {
            get => ReflectionCache.HoldPosition?.GetValue(_movement, null) is true;
            set => ReflectionCache.HoldPosition?.SetValue(_movement, value, null);
        }

        public bool FollowingPlayer => ReflectionCache.FollowingPlayer?.GetValue(_movement, null) is true;
        public bool Flying => ReflectionCache.Flying?.GetValue(_movement, null) is true;

        public int ControlGroup
        {
            get
            {
                if (_tagged == null || ReflectionCache.TaggedObjectTags == null)
                    return 0;
                var tags = ReflectionCache.TaggedObjectTags.GetValue(_tagged, null);
                foreach (var tag in Enumerate(tags))
                {
                    var g = ReflectionCache.ETagToGroup(Convert.ToInt32(tag));
                    if (g != 0)
                        return g;
                }

                return 0;
            }
            set
            {
                if (_tagged == null)
                    return;
                if (value is >= 1 and <= 3)
                {
                    var etag = ReflectionCache.EnumValue(ReflectionCache.ETagType, ReflectionCache.GroupToETag(value));
                    ReflectionCache.AddUnitGroupTag?.Invoke(_tagged, new[] { etag });
                    return;
                }

                foreach (var g in new[] { 1, 2, 3 })
                {
                    var etag = ReflectionCache.EnumValue(ReflectionCache.ETagType, ReflectionCache.GroupToETag(g));
                    ReflectionCache.RemoveUnitGroupTag?.Invoke(_tagged, new[] { etag });
                }
            }
        }

        public void SnapToNavmesh() =>
            ReflectionCache.SnapToNavmesh?.Invoke(_movement, Array.Empty<object>());

        public WorldVec GetNearestGroundPosition(WorldVec pos)
        {
            if (ReflectionCache.GetNearestGroundPosition == null)
                return pos;
            var v = ReflectionCache.GetNearestGroundPosition.Invoke(
                _movement, new[] { ReflectionCache.ToVector3(pos) });
            return v == null ? pos : ReflectionCache.FromVector3(v);
        }

        public void FollowPlayer(bool follow) =>
            ReflectionCache.FollowPlayer?.Invoke(_movement, new object[] { follow });
    }

    sealed class LiveSpawnLine : ISpawnLine
    {
        readonly object _line;

        public LiveSpawnLine(object line, int instanceId)
        {
            _line = line;
            InstanceId = instanceId;
        }

        public int InstanceId { get; }
        public string Name => GameObjectName(_line);

        public IReadOnlyList<WorldVec> Polyline
        {
            get
            {
                var points = new List<WorldVec>();
                object? spawnLine = _line;
                try
                {
                    spawnLine = ReflectionCache.SpawnLine?.GetValue(_line, null) ?? _line;
                }
                catch
                {
                    spawnLine = _line;
                }

                var countObj = ReflectionCache.GetMember(spawnLine, "childCount");
                var count = countObj is int n ? n : 0;
                if (count <= 0)
                {
                    points.Add(TransformPosition(spawnLine ?? _line));
                    return points;
                }

                for (var i = 0; i < count; i++)
                {
                    var child = ReflectionCache.CallNamed(spawnLine, "GetChild", i);
                    if (child != null)
                        points.Add(TransformPosition(child));
                }

                if (points.Count == 0)
                    points.Add(TransformPosition(spawnLine ?? _line));
                return points;
            }
        }
    }
}
