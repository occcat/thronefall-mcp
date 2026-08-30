using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class NextWaveTests
{
    [Fact]
    public void Include_without_nextWave_omits_it_from_json()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state?include=slots,units,spawns"));
        Assert.Equal(200, res.Status);
        Assert.DoesNotContain("\"nextWave\":", res.Body);
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.Null(dto!.NextWave);
        Assert.NotNull(dto.Spawns);
    }

    [Fact]
    public void Include_nextWave_in_day_returns_available()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state?include=nextWave"));
        Assert.Equal(200, res.Status);
        Assert.Contains("\"available\":", res.Body);
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.NotNull(dto!.NextWave);
        Assert.True(dto.NextWave!.Available);
        Assert.Null(dto.Slots);
        Assert.Null(dto.Spawns);
    }

    [Fact]
    public void Slice_next_wave_in_day_returns_available()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state/next-wave"));
        Assert.Equal(200, res.Status);
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.NotNull(dto!.NextWave);
        Assert.True(dto.NextWave!.Available);
        Assert.Equal(2, dto.NextWave.WaveNumber);
        Assert.Single(dto.NextWave.Groups);
        Assert.Equal("spawn", dto.NextWave.Groups[0].Spawn.Kind);
        Assert.Contains("\"mouths\":", res.Body);
        Assert.Single(dto.NextWave.Mouths);
        Assert.Equal("High Back Road", dto.NextWave.Mouths[0].Spawn.Name);
        Assert.Equal("E Melee", dto.NextWave.Mouths[0].Enemies[0].Name);
        Assert.Equal(8, dto.NextWave.Mouths[0].Enemies[0].Count);
    }

    [Fact]
    public void Unavailable_preview_does_not_fabricate_groups()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        world.Template.NextWave = new NextWaveDto { Available = false };
        using var _ = Push(new GameFacade(world));
        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state/next-wave"));
        Assert.Equal(200, res.Status);
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.NotNull(dto!.NextWave);
        Assert.False(dto.NextWave!.Available);
        Assert.Empty(dto.NextWave.Groups);
        Assert.Empty(dto.NextWave.Enemies);
        Assert.Empty(dto.NextWave.Mouths);
        Assert.Contains("\"mouths\":", res.Body);
        Assert.DoesNotContain("Front Road", res.Body);
    }

    [Fact]
    public void Slice_next_wave_in_menu_is_illegal_phase()
    {
        var world = new FakeWorld { HintsValue = Menu() };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state/next-wave"));
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.False(err!.Ok);
        Assert.Equal(ErrorCodes.IllegalPhase, err.Error);
        Assert.Equal(Phases.Menu, err.Phase);
    }

    [Fact]
    public void All_include_keeps_nextWave()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels") };
        Fill(world);
        var dto = new GameFacade(world).GetState();
        Assert.NotNull(dto.NextWave);
        Assert.True(dto.NextWave!.Available);
    }

    [Fact]
    public void Read_without_EnemySpawner_is_unavailable()
    {
        var ids = new IdRegistry();
        ids.BeginScene();
        var dto = NextWave.Read(ids);
        Assert.False(dto.Available);
        Assert.Empty(dto.Groups);
        Assert.Empty(dto.Enemies);
        Assert.Empty(dto.Mouths);
    }

    [Fact]
    public void GroupByMouth_merges_two_types_on_same_spawn()
    {
        var spawn = new EntityId { InstanceId = 88, Generation = 1, Kind = "spawn", Name = "High Back Road" };
        var mouths = NextWave.GroupByMouth(new[]
        {
            new NextWaveGroupDto { Spawn = spawn, EnemyName = "E Melee", Count = 8, SuggestedRally = new Vec3Dto { X = 1 } },
            new NextWaveGroupDto { Spawn = spawn, EnemyName = "E Archer", Count = 3, SuggestedRally = new Vec3Dto { X = 1 } }
        });
        Assert.Single(mouths);
        Assert.Equal(88, mouths[0].Spawn.InstanceId);
        Assert.Equal(2, mouths[0].Enemies.Count);
        Assert.Equal("E Melee", mouths[0].Enemies[0].Name);
        Assert.Equal(8, mouths[0].Enemies[0].Count);
        Assert.Equal("E Archer", mouths[0].Enemies[1].Name);
        Assert.Equal(3, mouths[0].Enemies[1].Count);
    }

    [Fact]
    public void GroupByMouth_adds_count_for_same_name_and_elite()
    {
        var spawn = new EntityId { InstanceId = 7, Kind = "spawn", Name = "Front Road" };
        var mouths = NextWave.GroupByMouth(new[]
        {
            new NextWaveGroupDto { Spawn = spawn, EnemyName = "E Melee", Elite = true, Count = 2, GoldCoins = 1 },
            new NextWaveGroupDto { Spawn = spawn, EnemyName = "E Melee", Elite = true, Count = 5, GoldCoins = 9 }
        });
        Assert.Single(mouths);
        Assert.Single(mouths[0].Enemies);
        Assert.Equal(7, mouths[0].Enemies[0].Count);
        Assert.True(mouths[0].Enemies[0].Elite);
        Assert.Equal(1, mouths[0].Enemies[0].GoldCoins);
    }

    [Fact]
    public void OpenApi_lists_next_wave_path()
    {
        var paths = Newtonsoft.Json.Linq.JObject.Parse(OpenApi.ToJson())["paths"];
        Assert.NotNull(paths!["/state/next-wave"]);
        var include = (string?)paths["/state"]?["get"]?["summary"];
        Assert.Contains("nextWave", include, StringComparison.Ordinal);
    }

    [Fact]
    public void Night_allows_next_wave_slice()
    {
        var world = new FakeWorld { HintsValue = InGame("Nordfels", timestate: "Night") };
        Fill(world);
        using var _ = Push(new GameFacade(world));
        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state/next-wave"));
        Assert.Equal(200, res.Status);
        Assert.True(Json.Deserialize<StateDto>(res.Body)!.NextWave!.Available);
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
            Economy = new EconomyDto { Balance = 12 },
            Clock = new ClockDto { Timestate = "Day", Wavenumber = 1, WaveCount = 12 },
            Spawns = new List<SpawnLineDto>
            {
                new() { Id = new EntityId { Kind = "spawn", Name = "Front Road" } }
            },
            NextWave = new NextWaveDto
            {
                Available = true,
                WaveNumber = 2,
                OutOfWaves = 12,
                GoldReward = 4,
                DifficultyMulti = 1f,
                WarningText = "They come from the north.",
                Groups =
                {
                    new NextWaveGroupDto
                    {
                        Spawn = new EntityId { InstanceId = 88, Generation = 1, Kind = "spawn", Name = "High Back Road" },
                        EnemyName = "E Melee",
                        Count = 8,
                        SuggestedRally = new Vec3Dto { X = -28, Y = 3, Z = 61 }
                    }
                },
                Enemies =
                {
                    new NextWaveEnemyDto { Name = "E Melee", Count = 8, MaxHp = 20 }
                }
            }
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
            dto.NextWave = Template.NextWave;
            dto.Cutters = Template.Cutters;
            _ = include;
            _ = facade;
        }
    }
}
