using System;
using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

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
    }

    public IdRegistry Ids { get; }
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
