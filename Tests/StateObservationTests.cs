using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Tests.Fakes;
using Xunit;
using static ThronefallControl.Tests.Fakes.ObservationFakeWorld;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class StateObservationTests
{
    [Fact]
    public void Tick_increments_generation_on_scene_change_and_stales_old_ids()
    {
        var world = new ObservationFakeWorld { HintsValue = InGame("Nordfels") };
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
        var world = new ObservationFakeWorld { HintsValue = Menu() };
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
        var world = new ObservationFakeWorld { HintsValue = InGame("Nordfels") };
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
        Assert.Null(dto.Training);
        Assert.Equal(12, dto.Economy.Balance);
        Assert.Equal("Day", dto.Clock.Timestate);
    }

    [Fact]
    public void Illegal_include_tokens_are_omitted()
    {
        var world = new ObservationFakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        var facade = new GameFacade(world);
        var dto = facade.GetState("slots,bogus,not-a-field");
        Assert.NotNull(dto.Slots);
        Assert.Null(dto.Units);
        Assert.Null(dto.Enemies);
        Assert.Null(dto.Spawns);
        Assert.Null(dto.Loadout);
        Assert.Null(dto.Cutters);
        Assert.Null(dto.Training);
    }

    [Fact]
    public void Http_include_omits_unrequested_fields_from_json()
    {
        var world = new ObservationFakeWorld { HintsValue = InGame("Nordfels") };
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
        Assert.DoesNotContain("\"training\":", res.Body);
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
        var world = new ObservationFakeWorld { HintsValue = Menu() };
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
        var world = new ObservationFakeWorld
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
        var training = router.Dispatch(RequestContext.Create("GET", "/state/training"));
        Assert.Equal(409, training.Status);
        var err = Json.Deserialize<ErrorResponse>(units.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
        Assert.Equal(Phases.EndScreen, err.Phase);
    }

    [Fact]
    public void GetState_in_menu_is_ok_and_loadout_slice_is_legal()
    {
        var world = new ObservationFakeWorld { HintsValue = Menu() };
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
    public void Clock_final_wave_and_score_appear_in_get_state()
    {
        var world = new ObservationFakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("GET", "/state"));
        Assert.Equal(200, res.Status);
        Assert.Contains("\"finalWaveComingUp\":true", res.Body.Replace(" ", ""));
        Assert.Contains("\"preFinalWaveComingUp\":true", res.Body.Replace(" ", ""));
        Assert.Contains("\"waveBeforeFinalWaveComingUp\":false", res.Body.Replace(" ", ""));
        Assert.Contains("\"currentScore\":120", res.Body.Replace(" ", ""));
        Assert.Contains("\"maxScorePerNight\":200", res.Body.Replace(" ", ""));
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.True(dto!.Clock.FinalWaveComingUp);
        Assert.True(dto.Clock.PreFinalWaveComingUp);
        Assert.False(dto.Clock.WaveBeforeFinalWaveComingUp);
        Assert.Equal(120, dto.Clock.CurrentScore);
        Assert.Equal(200, dto.Clock.MaxScorePerNight);
        Assert.NotNull(dto.Training);
        Assert.Single(dto.Training!);
    }

    [Fact]
    public void Include_slots_omits_training_from_json()
    {
        var world = new ObservationFakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("GET", "/state?include=slots"));
        Assert.Equal(200, res.Status);
        Assert.Contains("\"slots\":", res.Body);
        Assert.DoesNotContain("\"training\":", res.Body);
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.NotNull(dto!.Slots);
        Assert.Null(dto.Training);
    }

    [Fact]
    public void Training_include_and_slice_are_legal_in_day_illegal_in_menu()
    {
        var world = new ObservationFakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var router = Router.CreateDefault();

        var include = router.Dispatch(RequestContext.Create("GET", "/state?include=training"));
        Assert.Equal(200, include.Status);
        var includeDto = Json.Deserialize<StateDto>(include.Body);
        Assert.NotNull(includeDto!.Training);
        Assert.Single(includeDto.Training!);
        Assert.Equal(4412, includeDto.Training![0].SlotId);
        Assert.Equal("Barracks", includeDto.Training[0].BuildingName);
        Assert.True(includeDto.Training[0].HasKnockedOut);
        Assert.Equal(3.5f, includeDto.Training[0].TimeTillNextRespawn);
        Assert.Null(includeDto.Slots);
        Assert.Null(includeDto.Units);

        var slice = router.Dispatch(RequestContext.Create("GET", "/state/training"));
        Assert.Equal(200, slice.Status);
        var sliceDto = Json.Deserialize<StateDto>(slice.Body);
        Assert.NotNull(sliceDto!.Training);
        Assert.Single(sliceDto.Training!);

        world.HintsValue = Menu();
        var menu = router.Dispatch(RequestContext.Create("GET", "/state/training"));
        Assert.Equal(409, menu.Status);
        var err = Json.Deserialize<ErrorResponse>(menu.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
        Assert.Equal(Phases.Menu, err.Phase);
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

    static void Fill(ObservationFakeWorld world)
    {
        world.Template = new StateDto
        {
            Economy = new EconomyDto { Balance = 12, TrueBalance = 12, IsFreeToCallNight = true },
            Clock = new ClockDto
            {
                Timestate = "Day",
                Wavenumber = 1,
                WaveCount = 12,
                FinalWaveComingUp = true,
                PreFinalWaveComingUp = true,
                WaveBeforeFinalWaveComingUp = false,
                CurrentScore = 120,
                MaxScorePerNight = 200
            },
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
            Cutters = new List<CutterDto> { new() { ToggleCost = 8 } },
            Training = new List<TrainingDto>
            {
                new()
                {
                    SlotId = 4412,
                    BuildingName = "Barracks",
                    HasKnockedOut = true,
                    TimeTillNextRespawn = 3.5f
                }
            }
        };
    }

    enum TagManagerETag
    {
        NONE = 0,
        PlayerOwned = 1,
        Group2 = 46
    }
}
