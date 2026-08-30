using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Tests.Fakes;

public sealed class ObservationFakeWorld : IWorld
{
    public WorldHints HintsValue { get; set; } = new();
    public StateDto Template { get; set; } = new();

    public WorldHints Hints() => HintsValue;

    public void Capture(GameFacade facade, StateDto dto, StateInclude include)
    {
        dto.Level = Template.Level ?? new LevelDto { SceneName = HintsValue.SceneName };
        dto.Economy = Template.Economy;
        dto.Clock = Template.Clock;
        dto.King = Template.King;
        dto.Settings = Template.Settings;
        dto.Loadout = Template.Loadout;
        dto.Slots = Template.Slots;
        dto.Units = Template.Units;
        dto.Enemies = Template.Enemies;
        dto.Spawns = Template.Spawns;
        dto.NextWave = Template.NextWave;
        dto.Cutters = Template.Cutters;
        dto.Training = Template.Training;
        _ = include;
        _ = facade;
    }

    public static WorldHints Menu() => new()
    {
        SceneName = "_StartMenu",
        SceneState = "MainMenu"
    };

    public static WorldHints InGame(string scene = "Nordfels", string timestate = "Day") => new()
    {
        SceneName = scene,
        SceneState = "InGame",
        Timestate = timestate,
        MatchState = "InMatch"
    };

    public static IDisposable Push(GameFacade facade)
    {
        var previous = GameFacade.Current;
        GameFacade.Current = facade;
        return new CurrentScope(previous);
    }

    sealed class CurrentScope : IDisposable
    {
        readonly GameFacade _previous;

        public CurrentScope(GameFacade previous) => _previous = previous;

        public void Dispose() => GameFacade.Current = _previous;
    }
}
