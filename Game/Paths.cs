using System;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public interface IPathCutter
{
    int InstanceId { get; }
    string Name { get; }
    bool PathOpened { get; }
    int ToggleCost { get; }
    bool ToogleOnlyAtDay { get; }
    bool ToggleOnlyAtNight { get; }
    bool CanBeInteractedWith { get; }
    bool IsToggleValidToUse { get; }
    bool ToggleSupported { get; }
    bool HasBoundPathStateChanged { get; }
    bool HasToggleComplete { get; }
    void ToggleCutPath();
    void InvokePathStateChanged();
    void ToggleComplete();
}

public sealed class TogglePathResult
{
    public int Status { get; set; } = 200;
    public string? Error { get; set; }
    public string Message { get; set; } = "";
    public string Phase { get; set; } = "";
    public int Generation { get; set; }
    public bool DryRun { get; set; }
    public bool PathOpened { get; set; }
    public int ToggleCost { get; set; }
    public EntityId? Id { get; set; }
    public DryRunWouldDto? Would { get; set; }
}

public static class Paths
{
    public static TogglePathResult Toggle(EntityId? id, bool dryRun) =>
        Toggle(GameFacade.Current, id, dryRun);

    public static TogglePathResult Toggle(GameFacade game, EntityId? id, bool dryRun)
    {
        var world = game.World;
        var generation = game.Ids.SceneGeneration;
        var phase = world.Phase ?? "";

        if (world.SceneTransitionIsRunning || string.Equals(phase, "transition", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                409,
                ErrorCodes.TransitionInProgress,
                "scene transition in progress",
                "transition",
                generation);
        }

        if (id == null)
        {
            return Fail(
                404,
                ErrorCodes.NotFound,
                "missing cutter id",
                phase,
                generation);
        }

        if (!TryResolve(game, id, out var cutter, out var resolveError))
        {
            var status = resolveError == ErrorCodes.StaleId ? 409 : 404;
            var code = resolveError ?? ErrorCodes.NotFound;
            return Fail(
                status,
                code,
                code == ErrorCodes.StaleId ? "stale cutter id" : "cutter not found",
                phase,
                generation);
        }

        // Game field spelling is toogleOnlyAtDay.
        if (cutter!.ToogleOnlyAtDay && !string.Equals(phase, "day", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                409,
                ErrorCodes.IllegalPhase,
                $"POST /path/toggle is illegal in phase={phase} when toogleOnlyAtDay",
                phase,
                generation);
        }

        if (cutter.ToggleOnlyAtNight && !string.Equals(phase, "night", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                409,
                ErrorCodes.IllegalPhase,
                $"POST /path/toggle is illegal in phase={phase} when toggleOnlyAtNight",
                phase,
                generation);
        }

        if (!string.Equals(phase, "day", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(phase, "night", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                409,
                ErrorCodes.IllegalPhase,
                $"POST /path/toggle is illegal in phase={phase}",
                phase,
                generation);
        }

        if (!cutter.ToggleSupported)
        {
            return Fail(
                501,
                ErrorCodes.UnsupportedInThisBuild,
                "CutOpenPathInteractor.ToggleCutPath is unavailable",
                phase,
                generation);
        }

        var cost = cutter.ToggleCost;
        var blocked = !cutter.IsToggleValidToUse || world.Balance < cost;
        var entity = new EntityId
        {
            InstanceId = id.InstanceId,
            Generation = id.Generation,
            Kind = "cutter",
            Name = cutter.Name
        };

        if (dryRun)
        {
            return new TogglePathResult
            {
                Status = 200,
                Phase = phase,
                Generation = generation,
                DryRun = true,
                PathOpened = cutter.PathOpened,
                ToggleCost = cost,
                Id = entity,
                Would = new DryRunWouldDto
                {
                    Action = "toggle_path",
                    Cutter = cutter.Name,
                    Cost = cost,
                    BalanceAfter = blocked ? world.Balance : world.Balance - cost,
                    Blocked = blocked
                }
            };
        }

        if (world.Balance < cost)
        {
            return Fail(
                409,
                ErrorCodes.InsufficientGold,
                $"need {cost} gold to toggle path",
                phase,
                generation);
        }

        if (!cutter.IsToggleValidToUse)
        {
            return Fail(
                409,
                ErrorCodes.IllegalPhase,
                "cutter toggle is not valid to use (IsToggleValidToUse=false)",
                phase,
                generation);
        }

        if (cost > 0 && !world.SpendCoinsSupported)
        {
            return Fail(
                501,
                ErrorCodes.UnsupportedInThisBuild,
                "PlayerInteraction.SpendCoins is unavailable",
                phase,
                generation);
        }

        if (cost > 0)
            world.SpendCoins(cost);

        ApplyToggle(cutter);
        return new TogglePathResult
        {
            Status = 200,
            Phase = phase,
            Generation = generation,
            PathOpened = cutter.PathOpened,
            ToggleCost = cost,
            Id = entity
        };
    }

    static void ApplyToggle(IPathCutter cutter)
    {
        // ToggleComplete itself calls ToggleCutPath; only use it when the
        // pathStateChanged callback is unbound so we do not flip twice.
        if (cutter.HasBoundPathStateChanged)
        {
            cutter.ToggleCutPath();
            cutter.InvokePathStateChanged();
            return;
        }

        if (cutter.HasToggleComplete)
        {
            cutter.ToggleComplete();
            return;
        }

        cutter.ToggleCutPath();
    }

    static bool TryResolve(GameFacade game, EntityId id, out IPathCutter? cutter, out string? error)
    {
        cutter = null;
        if (game.Ids.TryResolve(id.InstanceId, id.Generation, out var target, out error))
        {
            cutter = AsCutter(target);
            if (cutter != null)
                return true;
            error = ErrorCodes.NotFound;
            return false;
        }

        if (error == ErrorCodes.StaleId)
            return false;

        if (id.Generation != game.Ids.SceneGeneration)
        {
            error = ErrorCodes.StaleId;
            return false;
        }

        cutter = FindLive(id.InstanceId);
        if (cutter == null)
        {
            error = ErrorCodes.NotFound;
            return false;
        }

        game.Ids.Register(id.InstanceId, "cutter", cutter.Name, cutter);
        error = null;
        return true;
    }

    static IPathCutter? AsCutter(object? target)
    {
        if (target is IPathCutter cutter)
            return cutter;
        if (target == null)
            return null;
        if (ReflectionCache.CutOpenPathInteractorType?.IsInstanceOfType(target) == true)
            return new ReflectedCutter(target);
        return null;
    }

    static IPathCutter? FindLive(int instanceId)
    {
        foreach (var obj in ReflectionCache.FindCutters())
        {
            if (obj == null)
                continue;
            if (ReflectionCache.GetGameObjectInstanceId(obj) != instanceId)
                continue;
            return new ReflectedCutter(obj);
        }

        return null;
    }

    static TogglePathResult Fail(int status, string error, string message, string phase, int generation) =>
        new()
        {
            Status = status,
            Error = error,
            Message = message,
            Phase = phase,
            Generation = generation
        };

    sealed class ReflectedCutter : IPathCutter
    {
        readonly object _target;

        public ReflectedCutter(object target) => _target = target;

        public int InstanceId => ReflectionCache.GetGameObjectInstanceId(_target);

        public string Name => ReflectionCache.GetObjectName(_target);

        public bool PathOpened => ReflectionCache.ReadBool(ReflectionCache.PathOpened, _target);

        public int ToggleCost => ReflectionCache.ReadInt(ReflectionCache.ToggleCost, _target);

        public bool ToogleOnlyAtDay => ReflectionCache.ReadBool(ReflectionCache.ToogleOnlyAtDay, _target);

        public bool ToggleOnlyAtNight => ReflectionCache.ReadBool(ReflectionCache.ToggleOnlyAtNight, _target);

        public bool CanBeInteractedWith =>
            ReflectionCache.ReadBool(ReflectionCache.CanBeInteractedWith, _target, fallback: true);

        public bool IsToggleValidToUse
        {
            get
            {
                if (ReflectionCache.IsToggleValidToUse != null)
                    return ReflectionCache.InvokeBool(ReflectionCache.IsToggleValidToUse, _target);
                return CanBeInteractedWith;
            }
        }

        public bool ToggleSupported =>
            ReflectionCache.ToggleCutPath != null || ReflectionCache.ToggleComplete != null;

        public bool HasBoundPathStateChanged
        {
            get
            {
                if (ReflectionCache.PathStateChanged == null)
                    return false;
                try
                {
                    return ReflectionCache.PathStateChanged.GetValue(_target) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool HasToggleComplete => ReflectionCache.ToggleComplete != null;

        public void ToggleCutPath()
        {
            if (ReflectionCache.ToggleCutPath == null)
                throw new InvalidOperationException("CutOpenPathInteractor.ToggleCutPath is unavailable");
            ReflectionCache.Invoke(ReflectionCache.ToggleCutPath, _target);
        }

        public void InvokePathStateChanged()
        {
            object? callback = null;
            try
            {
                callback = ReflectionCache.PathStateChanged?.GetValue(_target);
            }
            catch
            {
                callback = null;
            }

            ReflectionCache.InvokeBoolCallback(callback, PathOpened);
        }

        public void ToggleComplete()
        {
            if (ReflectionCache.ToggleComplete == null)
                throw new InvalidOperationException("CutOpenPathInteractor.ToggleComplete is unavailable");
            ReflectionCache.Invoke(ReflectionCache.ToggleComplete, _target);
        }
    }
}
