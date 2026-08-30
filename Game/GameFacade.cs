using System;

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

    public IdRegistry Ids { get; set; } = new();

    public IGameWorld World { get; set; } = new LiveGameWorld();
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
