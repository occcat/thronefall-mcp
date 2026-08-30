using System;
using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public interface IGameWorld
{
    string Phase { get; }
    bool SceneTransitionIsRunning { get; }
    bool IsFreeToCallNight { get; }
    int Balance { get; }
    bool SwitchToNightSupported { get; }
    bool SpendCoinsSupported { get; }
    void SwitchToNight();
    void SpendCoins(int amount);
}

public sealed class GameFacade
{
    public static GameFacade Current { get; set; } = new();

    readonly IWorld _world;
    string? _sceneKey;
    bool _started;

    public GameFacade() : this(new LiveWorld()) { }

    public GameFacade(IWorld world) : this(world, null) { }

    public GameFacade(IUnitWorld unitWorld) : this(null, unitWorld) { }

    GameFacade(IWorld? world, IUnitWorld? unitWorld)
    {
        _world = world ?? new LiveWorld();
        Ids = new IdRegistry();
        Phase = Phases.Boot;
        NightPolicy = string.IsNullOrEmpty(PluginConfig.DefaultNightPolicy)
            ? NightPolicies.Human
            : PluginConfig.DefaultNightPolicy;
        Units = new Units(unitWorld ?? new LiveUnitWorld(this));
        World = new LiveGameWorld();
    }

    public IdRegistry Ids { get; set; }
    public IGameWorld World { get; set; }
    public Units Units { get; }
    public string Phase { get; private set; }
    public string Scene { get; private set; } = "";
    public string NightPolicy { get; set; }

    public void Tick()
    {
        WorldHints hints;
        try
        {
            hints = _world.Hints() ?? new WorldHints();
        }
        catch
        {
            hints = new WorldHints();
        }

        var key = (hints.SceneName ?? "") + "\n" + (hints.SceneState ?? "");
        if (!_started)
        {
            Ids.BeginScene();
            _started = true;
            _sceneKey = key;
        }
        else if (!string.Equals(_sceneKey, key, StringComparison.Ordinal))
        {
            Ids.BeginScene();
            _sceneKey = key;
        }

        Phase = Phases.From(hints);
        if (!string.IsNullOrEmpty(hints.SceneName))
            Scene = hints.SceneName;
        else if (Phase == Phases.Menu)
            Scene = Scene.Length == 0 ? "_StartMenu" : Scene;
        else if (Phase == Phases.LevelSelect && Scene.Length == 0)
            Scene = "_LevelSelect";
    }

    public StateDto GetState(string? include = null)
    {
        Tick();
        var filter = StateInclude.Parse(include);
        var dto = NewCore();
        try
        {
            _world.Capture(this, dto, filter);
        }
        catch
        {
            // empty-safe snapshot
        }

        filter.OmitUnrequested(dto);
        dto.Ok = true;
        dto.Generation = Ids.SceneGeneration;
        dto.Phase = Phase;
        dto.Scene = Scene;
        dto.NightPolicy = NightPolicy;
        return dto;
    }

    public bool TryGetSlice(string slice, out StateDto? dto, out string? error, out string? message)
    {
        Tick();
        dto = null;
        error = null;
        message = null;
        if (!Phases.AllowsSlice(Phase, slice))
        {
            error = ErrorCodes.IllegalPhase;
            message = $"GET /state/{slice} is illegal in phase={Phase}";
            return false;
        }

        dto = GetState(slice);
        return true;
    }

    public static GameFacade CreateLive()
    {
        var facade = new GameFacade();
        Current = facade;
        Units.Current = facade.Units;
        return facade;
    }

    StateDto NewCore() => new()
    {
        Ok = true,
        Generation = Ids.SceneGeneration,
        Phase = Phase,
        Scene = Scene,
        NightPolicy = NightPolicy
    };
}

public sealed class LiveGameWorld : IGameWorld
{
    public string Phase => DetectPhase();

    public bool SceneTransitionIsRunning
    {
        get
        {
            var stm = ReflectionCache.GetSceneTransitionManager();
            return stm != null && ReflectionCache.ReadBool(ReflectionCache.SceneTransitionIsRunning, stm);
        }
    }

    public bool IsFreeToCallNight
    {
        get
        {
            var pi = ReflectionCache.GetPlayerInteraction();
            return pi != null && ReflectionCache.ReadBool(ReflectionCache.PlayerInteractionIsFreeToCallNight, pi);
        }
    }

    public int Balance
    {
        get
        {
            var pi = ReflectionCache.GetPlayerInteraction();
            return pi == null ? 0 : ReflectionCache.ReadInt(ReflectionCache.PlayerInteractionBalance, pi);
        }
    }

    public bool SwitchToNightSupported => ReflectionCache.DayNightCycleSwitchToNight != null;

    public bool SpendCoinsSupported => ReflectionCache.PlayerInteractionSpendCoins != null;

    public void SwitchToNight()
    {
        var cycle = ReflectionCache.GetDayNightCycle();
        if (cycle == null || ReflectionCache.DayNightCycleSwitchToNight == null)
            throw new InvalidOperationException("DayNightCycle.SwitchToNight is unavailable");
        // Only SwitchToNight. Never EnemySpawner.DebugSkipWave / NightCall fill.
        ReflectionCache.Invoke(ReflectionCache.DayNightCycleSwitchToNight, cycle);
    }

    public void SpendCoins(int amount)
    {
        if (amount == 0)
            return;
        var pi = ReflectionCache.GetPlayerInteraction();
        if (pi == null || ReflectionCache.PlayerInteractionSpendCoins == null)
            throw new InvalidOperationException("PlayerInteraction.SpendCoins is unavailable");
        ReflectionCache.Invoke(ReflectionCache.PlayerInteractionSpendCoins, pi, amount);
    }

    static string DetectPhase()
    {
        var stm = ReflectionCache.GetSceneTransitionManager();
        if (stm != null)
        {
            if (ReflectionCache.ReadBool(ReflectionCache.SceneTransitionIsRunning, stm))
                return "transition";

            if (ReflectionCache.InvokeBool(ReflectionCache.IsInLevelSelect, stm))
                return "level_select";

            var scene = ReflectionCache.ReadName(ReflectionCache.CurrentSceneState, stm);
            if (string.Equals(scene, "MainMenu", StringComparison.OrdinalIgnoreCase))
                return "menu";
            if (string.Equals(scene, "LevelSelect", StringComparison.OrdinalIgnoreCase))
                return "level_select";
            if (!string.Equals(scene, "InGame", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(scene))
                return scene.ToLowerInvariant();
        }

        var cycle = ReflectionCache.GetDayNightCycle();
        if (cycle != null)
        {
            var time = ReflectionCache.ReadName(ReflectionCache.DayNightCycleCurrentTimestate, cycle);
            if (string.Equals(time, "Night", StringComparison.OrdinalIgnoreCase))
                return "night";
            if (string.Equals(time, "Day", StringComparison.OrdinalIgnoreCase))
                return "day";
        }

        return "unknown";
    }
}
