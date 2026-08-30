namespace ThronefallControl.Game;

public static class Phases
{
    public const string Boot = "boot";
    public const string Transition = "transition";
    public const string Menu = "menu";
    public const string LevelSelect = "level_select";
    public const string Day = "day";
    public const string Night = "night";
    public const string EndScreen = "end_screen";

    public static string From(WorldHints hints)
    {
        if (hints.TransitionRunning)
            return Transition;

        var sceneState = hints.SceneState ?? "";
        if (sceneState == "MainMenu")
            return Menu;
        if (sceneState == "LevelSelect" || hints.InLevelSelect)
            return LevelSelect;

        var inGame = sceneState == "InGame";
        if (inGame || sceneState.Length == 0)
        {
            if (IsEndScreen(hints))
                return EndScreen;
            if (hints.Timestate == "Night")
                return Night;
            if (hints.Timestate == "Day")
                return Day;
            if (inGame)
                return Day;
        }

        return Boot;
    }

    public static bool AllowsSlice(string phase, string slice)
    {
        switch (slice)
        {
            case StateInclude.Slots:
                return phase == Day || phase == Night || phase == EndScreen;
            case StateInclude.Units:
            case StateInclude.Training:
            case StateInclude.Enemies:
            case StateInclude.Spawns:
            case StateInclude.NextWave:
                return phase == Day || phase == Night;
            case StateInclude.Loadout:
                return phase == Menu || phase == LevelSelect || phase == Day || phase == Night;
            default:
                return true;
        }
    }

    static bool IsEndScreen(WorldHints hints)
    {
        if (hints.EndScreenVisible)
            return true;
        var match = hints.MatchState ?? "";
        return match.StartsWith("AfterMatch");
    }
}
