using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public interface IWorld
{
    WorldHints Hints();
    void Capture(GameFacade facade, StateDto dto, StateInclude include);
}

public sealed class WorldHints
{
    public bool TransitionRunning { get; set; }
    public string SceneState { get; set; } = "";
    public bool InLevelSelect { get; set; }
    public string Timestate { get; set; } = "";
    public string SceneName { get; set; } = "";
    public string MatchState { get; set; } = "";
    public bool EndScreenVisible { get; set; }
}
