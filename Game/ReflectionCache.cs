using System;

namespace ThronefallControl.Game;

public static class ReflectionCache
{
    public static void TryInit(object? logger = null)
    {
        _ = logger;
        try
        {
            UnityAccess.Warmup();
            _ = UnityAccess.FindField("BuildingInteractor", "harvestedToday");
            _ = UnityAccess.FindField("BuildSlot", "requiredRoot");
            _ = UnityAccess.FindField("CutOpenPathInteractor", "pathOpened");
            _ = UnityAccess.FindField("CutOpenPathInteractor", "toggleCost");
            _ = UnityAccess.FindMethod("CutOpenPathInteractor", "IsToggleValidToUse");
            _ = UnityAccess.FindField("MatchSave", "currentLoadoutAsString");
            _ = UnityAccess.FindField("EnemySpawnLine", "difficulty");
        }
        catch
        {
            // missing private members must not prevent plugin start
        }
    }
}
