using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Tests.GameFakes;
using Xunit;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class LoadoutCatalogTests : IDisposable
{
    GameFacade? _previous;

    public void Dispose()
    {
        if (_previous != null)
            GameFacade.Current = _previous;
        Loadout.Reset();
        RuntimeState.Reset();
    }

    [Fact]
    public void Get_state_loadout_json_includes_catalog_and_quests()
    {
        var world = new FakeWorld { HintsValue = Menu() };
        world.Template.Loadout = new LoadoutDto
        {
            AsString = { "Royal Mint" },
            PerkPointsRemaining = 1,
            Catalog =
            {
                new LoadoutItemDto
                {
                    Name = "Royal Mint",
                    Kind = "perk",
                    Locked = false,
                    Unlocked = true,
                    Description = "Start with extra gold"
                },
                new LoadoutItemDto
                {
                    Name = "God King",
                    Kind = "perk",
                    Locked = true,
                    Unlocked = false,
                    Description = "Locked"
                }
            },
            Quests = { new QuestDto { Statement = "Beat the level", Complete = true } },
            Worth = 12
        };
        Push(new GameFacade(world));

        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state/loadout"));
        Assert.Equal(200, res.Status);
        Assert.Contains("\"catalog\":", res.Body);
        Assert.Contains("\"quests\":", res.Body);
        Assert.Contains("\"worth\":12", res.Body);
        Assert.Contains("God King", res.Body);
        Assert.Contains("Beat the level", res.Body);

        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.NotNull(dto!.Loadout);
        Assert.Contains("Royal Mint", dto.Loadout!.AsString);
        Assert.Equal(2, dto.Loadout.Catalog.Count);
        Assert.Equal("perk", dto.Loadout.Catalog[0].Kind);
        Assert.False(dto.Loadout.Catalog[0].Locked);
        Assert.True(dto.Loadout.Catalog[1].Locked);
        Assert.False(dto.Loadout.Catalog[1].Unlocked);
        Assert.Equal("Beat the level", dto.Loadout.Quests[0].Statement);
        Assert.True(dto.Loadout.Quests[0].Complete);
        Assert.Equal(12, dto.Loadout.Worth);
    }

    [Fact]
    public void Include_without_loadout_omits_catalog()
    {
        var world = new FakeWorld { HintsValue = InGame() };
        world.Template.Loadout = new LoadoutDto
        {
            Catalog = { new LoadoutItemDto { Name = "Royal Mint", Kind = "perk" } },
            Quests = { new QuestDto { Statement = "Beat the level" } }
        };
        world.Template.Slots = new List<SlotDto>
        {
            new() { BuildingName = "House" }
        };
        Push(new GameFacade(world));

        var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state?include=slots"));
        Assert.Equal(200, res.Status);
        Assert.Contains("\"slots\":", res.Body);
        Assert.DoesNotContain("\"loadout\":", res.Body);
        Assert.DoesNotContain("\"catalog\":", res.Body);
        Assert.DoesNotContain("\"quests\":", res.Body);
        var dto = Json.Deserialize<StateDto>(res.Body);
        Assert.Null(dto!.Loadout);
    }

    [Fact]
    public void MapCatalog_sets_kind_and_locked()
    {
        var items = LoadoutCatalog.MapCatalog(new[]
        {
            new LoadoutCatalog.Source { Name = "Royal Mint", Kind = "perk", Locked = false },
            new LoadoutCatalog.Source { Name = "God King", Kind = "Perk", Locked = true, Description = "meta" },
            new LoadoutCatalog.Source { Name = "Light Spear", Kind = "weapon", Locked = false },
            new LoadoutCatalog.Source { Name = "No Towers", Kind = "mutator", Locked = true }
        });

        Assert.Equal(4, items.Count);
        Assert.Equal("perk", items[0].Kind);
        Assert.False(items[0].Locked);
        Assert.True(items[0].Unlocked);
        Assert.Equal("perk", items[1].Kind);
        Assert.True(items[1].Locked);
        Assert.False(items[1].Unlocked);
        Assert.Equal("meta", items[1].Description);
        Assert.Equal("weapon", items[2].Kind);
        Assert.Equal("mutator", items[3].Kind);
        Assert.True(items[3].Locked);
    }

    [Fact]
    public void MapUiItem_reads_tfui_locked_and_kind()
    {
        var locked = new TFUIEquippable
        {
            equippableData = { displayName = "God King", description = "meta perk", IsUnlocked = false },
            locked = true,
            isPerk = true
        };
        var item = LoadoutCatalog.MapUiItem(locked, "weapon");
        Assert.NotNull(item);
        Assert.Equal("God King", item!.Name);
        Assert.Equal("perk", item.Kind);
        Assert.True(item.Locked);
        Assert.False(item.Unlocked);
        Assert.Equal("meta perk", item.Description);
    }

    [Fact]
    public void MapEquippable_uses_runtime_type_for_kind()
    {
        var perk = LoadoutCatalog.MapEquippable(new EquippablePerk
        {
            displayName = "Royal Mint",
            description = "gold",
            IsUnlocked = true
        });
        Assert.Equal("perk", perk!.Kind);
        Assert.False(perk.Locked);
        Assert.True(perk.Unlocked);

        var weapon = LoadoutCatalog.MapEquippable(new EquippableWeapon
        {
            displayName = "Light Spear",
            IsUnlocked = false
        });
        Assert.Equal("weapon", weapon!.Kind);
        Assert.True(weapon.Locked);

        Assert.Null(LoadoutCatalog.MapEquippable(new PerkPoint { displayName = "Perk Point" }));
    }

    [Fact]
    public void Fill_reads_pascal_case_Quests_property()
    {
        var info = new LevelInfoWithQuestsProperty();
        info.Quests.Add(new Quest { statement = "Beat the level" });

        var fromRead = LoadoutCatalog.ReadQuests(info);
        Assert.Equal("Beat the level", Assert.Single(fromRead).Statement);

        var dto = new LoadoutDto();
        LoadoutCatalog.Fill(dto, info);
        Assert.Equal("Beat the level", Assert.Single(dto.Quests).Statement);
        Assert.False(string.IsNullOrWhiteSpace(dto.Quests[0].Statement));
    }

    [Fact]
    public void MapQuest_reads_statement_and_check_beaten()
    {
        var quest = new Quest { statement = "Beat without mutators", beaten = true };
        var dto = LoadoutCatalog.MapQuest(quest, new LevelData());
        Assert.Equal("Beat without mutators", dto!.Statement);
        Assert.True(dto.Complete);

        var unread = LoadoutCatalog.MapQuest(
            new Quest { statement = "Score 10k", beaten = true },
            levelData: null);
        Assert.Equal("Score 10k", unread!.Statement);
        Assert.False(unread.Complete);
    }

    [Fact]
    public void Worth_is_omitted_when_null()
    {
        var json = Json.Serialize(new LoadoutDto { AsString = { "Royal Mint" } });
        Assert.DoesNotContain("worth", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"catalog\":[]", json);
        Assert.Contains("\"quests\":[]", json);
    }

    void Push(GameFacade facade)
    {
        _previous = GameFacade.Current;
        GameFacade.Current = facade;
    }

    static WorldHints Menu() => new()
    {
        SceneName = "_StartMenu",
        SceneState = "MainMenu"
    };

    static WorldHints InGame() => new()
    {
        SceneName = "Nordfels",
        SceneState = "InGame",
        Timestate = "Day",
        MatchState = "InMatch"
    };

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

    sealed class LevelInfoWithQuestsProperty
    {
        readonly List<Quest> _quests = new();
        public List<Quest> Quests => _quests;
    }

    sealed class EquippablePerk
    {
        public string displayName = "";
        public string description = "";
        public bool IsUnlocked { get; set; } = true;
    }

    sealed class EquippableWeapon
    {
        public string displayName = "";
        public string description = "";
        public bool IsUnlocked { get; set; } = true;
    }

    sealed class PerkPoint
    {
        public string displayName = "";
        public bool IsUnlocked { get; set; } = true;
    }
}
