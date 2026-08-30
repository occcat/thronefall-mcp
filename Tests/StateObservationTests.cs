using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class StateObservationTests
{
    [Fact]
    public void Tick_increments_generation_on_scene_change_and_stales_old_ids()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        var facade = new GameFacade(world);
        facade.Tick();
        var firstGen = facade.Ids.SceneGeneration;
        var marker = new object();
        var id = facade.Ids.Register(42, "slot", "House", marker);
        Assert.Equal(firstGen, id.Generation);
        Assert.True(facade.Ids.TryResolve(id, out var found, out var error));
        Assert.Same(marker, found);
        Assert.Null(error);
        facade.Tick();
        Assert.Equal(firstGen, facade.Ids.SceneGeneration);

        world.HintsValue = InGame("Durststein");
        facade.Tick();
        Assert.True(facade.Ids.SceneGeneration > firstGen);
        Assert.False(facade.Ids.TryResolve(id.InstanceId, id.Generation, out _, out error));
        Assert.Equal(ErrorCodes.StaleId, error);
        Assert.False(facade.Ids.TryResolve(42, facade.Ids.SceneGeneration, out _, out error));
        Assert.Equal(ErrorCodes.NotFound, error);
    }

    [Fact]
    public void GetState_never_refuses_and_detects_phase()
    {
        var world = new FakeWorld { HintsValue = Menu() };
        Fill(world);
        var facade = new GameFacade(world);
        var dto = facade.GetState();
        Assert.True(dto.Ok);
        Assert.Equal(Phases.Menu, dto.Phase);
        Assert.True(dto.Generation >= 1);

        world.HintsValue = InGame("Nordfels", timestate: "Night");
        dto = facade.GetState();
        Assert.True(dto.Ok);
        Assert.Equal(Phases.Night, dto.Phase);

        world.HintsValue = new WorldHints
        {
            TransitionRunning = true,
            SceneName = "Nordfels",
            SceneState = "InGame",
            Timestate = "Day"
        };
        dto = facade.GetState();
        Assert.True(dto.Ok);
        Assert.Equal(Phases.Transition, dto.Phase);
    }

    [Fact]
    public void Include_keeps_requested_slices_and_omits_the_rest()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        var facade = new GameFacade(world);
        var dto = facade.GetState("slots,units,spawns");
        Assert.NotNull(dto.Slots);
        Assert.Single(dto.Slots!);
        Assert.Equal("House", dto.Slots![0].BuildingName);
        Assert.NotNull(dto.Units);
        Assert.Single(dto.Units!);
        Assert.NotNull(dto.Spawns);
        Assert.Null(dto.Enemies);
        Assert.Null(dto.Loadout);
        Assert.Null(dto.Cutters);
        Assert.Equal(12, dto.Economy.Balance);
        Assert.Equal("Day", dto.Clock.Timestate);
    }

    [Fact]
    public void Illegal_include_tokens_are_omitted()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        var facade = new GameFacade(world);
        var dto = facade.GetState("slots,bogus,not-a-field");
        Assert.NotNull(dto.Slots);
        Assert.Null(dto.Units);
        Assert.Null(dto.Enemies);
        Assert.Null(dto.Spawns);
        Assert.Null(dto.Loadout);
        Assert.Null(dto.Cutters);
    }

    [Fact]
    public void Http_include_omits_unrequested_fields_from_json()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("GET", "/state?include=slots,units,spawns"));
        Assert.Equal(200, res.Status);
        Assert.Contains("\"slots\":", res.Body);
        Assert.Contains("\"units\":", res.Body);
        Assert.Contains("\"spawns\":", res.Body);
        Assert.DoesNotContain("\"enemies\":", res.Body);
        Assert.DoesNotContain("\"loadout\":", res.Body);
        Assert.DoesNotContain("\"cutters\":", res.Body);
        Assert.DoesNotContain("bogus", res.Body);
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.True(dto!.Ok);
        Assert.Equal(Phases.Day, dto.Phase);
        Assert.Null(dto.Enemies);
        Assert.Null(dto.Loadout);
    }

    [Fact]
    public void Slice_in_illegal_phase_returns_error_envelope()
    {
        var world = new FakeWorld { HintsValue = Menu() };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("GET", "/state/slots"));
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.False(err!.Ok);
        Assert.Equal(ErrorCodes.IllegalPhase, err.Error);
        Assert.Equal(Phases.Menu, err.Phase);
        Assert.True(err.Generation >= 1);
        Assert.Contains("illegal in phase=menu", err.Message);
        Assert.Contains("\"ok\":false", res.Body.Replace(" ", ""));
        Assert.Contains("\"error\":\"illegal_phase\"", res.Body.Replace(" ", ""));
    }

    [Fact]
    public void End_screen_allows_slots_but_not_units()
    {
        var world = new FakeWorld
        {
            HintsValue = new WorldHints
            {
                SceneName = "Nordfels",
                SceneState = "InGame",
                Timestate = "Night",
                MatchState = "AfterMatchVictory"
            }
        };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var router = Router.CreateDefault();
        Assert.Equal(200, router.Dispatch(RequestContext.Create("GET", "/state/slots")).Status);
        var units = router.Dispatch(RequestContext.Create("GET", "/state/units"));
        Assert.Equal(409, units.Status);
        var err = Json.Deserialize<ErrorResponse>(units.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
        Assert.Equal(Phases.EndScreen, err.Phase);
    }

    [Fact]
    public void GetState_in_menu_is_ok_and_loadout_slice_is_legal()
    {
        var world = new FakeWorld { HintsValue = Menu() };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var router = Router.CreateDefault();
        var full = router.Dispatch(RequestContext.Create("GET", "/state"));
        Assert.Equal(200, full.Status);
        var dto = Json.Deserialize<StateDto>(full.Body);
        Assert.True(dto!.Ok);
        Assert.Equal(Phases.Menu, dto.Phase);

        var loadout = router.Dispatch(RequestContext.Create("GET", "/state/loadout"));
        Assert.Equal(200, loadout.Status);
        var slice = Json.Deserialize<StateDto>(loadout.Body);
        Assert.NotNull(slice!.Loadout);
        Assert.Contains("Royal Mint", slice.Loadout!.AsString);
        Assert.Null(slice.Slots);
    }

    [Fact]
    public void ETag_names_follow_design_table()
    {
        Assert.Equal("PlayerOwned", ETags.NameOf(1));
        Assert.Equal("MeeleFighter", ETags.NameOf(5));
        Assert.Equal("Group1", ETags.NameOf(45));
        var names = new List<string>();
        var ids = new List<int>();
        ETags.Add(TagManagerETag.PlayerOwned, names, ids);
        ETags.Add(TagManagerETag.Group2, names, ids);
        Assert.Equal(new[] { "PlayerOwned", "Group2" }, names);
        Assert.Equal(2, ETags.ControlGroup(new List<string> { "PlayerUnit", "Group2" }));
    }

    static WorldHints Menu() => new()
    {
        SceneName = "_StartMenu",
        SceneState = "MainMenu"
    };

    static WorldHints InGame(string scene, string timestate = "Day") => new()
    {
        SceneName = scene,
        SceneState = "InGame",
        Timestate = timestate,
        MatchState = "InMatch"
    };

    static void Fill(FakeWorld world)
    {
        world.Template = new StateDto
        {
            Economy = new EconomyDto { Balance = 12, TrueBalance = 12, IsFreeToCallNight = true },
            Clock = new ClockDto { Timestate = "Day", Wavenumber = 1, WaveCount = 12 },
            King = new KingDto { Hp = 105, MaxHp = 105, Alive = true },
            Settings = new SettingsDto { ResetUnitFormationEveryMorning = true, EnableControlGroups = true },
            Loadout = new LoadoutDto { AsString = { "Royal Mint" }, PerkPointsRemaining = 1 },
            Slots = new List<SlotDto>
            {
                new()
                {
                    Id = new EntityId { InstanceId = 4412, Generation = 1, Kind = "slot", Name = "House" },
                    BuildingName = "House",
                    NextUpgradeOrBuildCost = 2,
                    Position = new Vec3Dto { X = 1, Z = -1 }
                }
            },
            Units = new List<UnitDto>
            {
                new()
                {
                    TypeName = "P Knight",
                    Tags = { "PlayerOwned", "PlayerUnit" },
                    TagIds = { 1, 8 },
                    HoldPosition = true
                }
            },
            Enemies = new EnemySummaryDto { Count = 1, Units = { new EnemyDto { Name = "Ogre" } } },
            Spawns = new List<SpawnLineDto>
            {
                new() { Difficulty = "Normal", Polyline = { new Vec3Dto { X = 20 } } }
            },
            Cutters = new List<CutterDto> { new() { ToggleCost = 8 } }
        };
    }

    static CurrentScope Push(GameFacade facade)
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

    enum TagManagerETag
    {
        NONE = 0,
        PlayerOwned = 1,
        Group2 = 46
    }

    sealed class FakeWorld : IWorld
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
            dto.Cutters = Template.Cutters;
            _ = include;
            _ = facade;
        }
    }
}
