using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class SlotPreviewTests
{
    [Fact]
    public void Apply_copies_tooltip_label_and_unlock_lists()
    {
        var dto = new SlotDto { BuildingName = "House" };
        SlotPreview.Apply(
            dto,
            "Upgrade House: +1 gold",
            "House L2",
            new[] { "Mill", "Tower" },
            new[] { 4418, 4420 });

        Assert.Equal("Upgrade House: +1 gold", dto.Tooltip);
        Assert.Equal("House L2", dto.NextUpgradeLabel);
        Assert.Equal(new[] { "Mill", "Tower" }, dto.UnlockPreview.BuildingNames);
        Assert.Equal(new[] { 4418, 4420 }, dto.UnlockPreview.SlotIds);
        Assert.Equal("House", dto.BuildingName);
    }

    [Fact]
    public void Apply_null_and_empty_inputs_do_not_throw()
    {
        var dto = new SlotDto
        {
            Tooltip = "stale",
            NextUpgradeLabel = "stale",
            UnlockPreview = new SlotUnlockPreviewDto
            {
                BuildingNames = { "old" },
                SlotIds = { 1 }
            }
        };

        SlotPreview.Apply(dto, null, null, null, null);
        Assert.Equal("", dto.Tooltip);
        Assert.Equal("", dto.NextUpgradeLabel);
        Assert.Empty(dto.UnlockPreview.BuildingNames);
        Assert.Empty(dto.UnlockPreview.SlotIds);

        SlotPreview.Apply(dto, "", "", Array.Empty<string>(), Array.Empty<int>());
        Assert.Equal("", dto.Tooltip);
        Assert.Equal("", dto.NextUpgradeLabel);
        Assert.Empty(dto.UnlockPreview.BuildingNames);
        Assert.Empty(dto.UnlockPreview.SlotIds);
    }

    [Fact]
    public void Fill_null_slot_clears_preview_fields()
    {
        var dto = new SlotDto
        {
            Tooltip = "stale",
            NextUpgradeLabel = "stale",
            UnlockPreview = new SlotUnlockPreviewDto { BuildingNames = { "old" }, SlotIds = { 1 } }
        };
        SlotPreview.Fill(dto, null, null);
        Assert.Equal("", dto.Tooltip);
        Assert.Equal("", dto.NextUpgradeLabel);
        Assert.Empty(dto.UnlockPreview.BuildingNames);
        Assert.Empty(dto.UnlockPreview.SlotIds);
    }

    [Fact]
    public void Fill_reads_tooltip_and_mapped_unlock_ids()
    {
        var slot = new FakeBuildSlot();
        var map = new Dictionary<object, int> { [slot.Unlock] = 4418 };
        var dto = new SlotDto();
        SlotPreview.Fill(dto, slot, map);
        Assert.Equal("full tooltip", dto.Tooltip);
        Assert.Equal(new[] { "Mill" }, dto.UnlockPreview.BuildingNames);
        Assert.Equal(new[] { 4418 }, dto.UnlockPreview.SlotIds);
        Assert.Equal("", dto.NextUpgradeLabel);
    }

    [Fact]
    public void Fill_omits_unlock_id_when_not_in_instance_map()
    {
        var dto = new SlotDto();
        SlotPreview.Fill(dto, new FakeBuildSlot(), new Dictionary<object, int>());
        Assert.Equal(new[] { "Mill" }, dto.UnlockPreview.BuildingNames);
        Assert.Empty(dto.UnlockPreview.SlotIds);
    }

    [Fact]
    public void Fill_uses_choice_name_when_next_upgrade_is_choice()
    {
        var dto = new SlotDto();
        SlotPreview.Fill(dto, new FakeChoiceSlot(), null);
        Assert.Equal("Archers / Knights", dto.NextUpgradeLabel);
    }

    [Fact]
    public void MapUnlock_skips_null_or_empty_building_names()
    {
        var preview = SlotPreview.MapUnlock(new[] { "Mill", "", null! }, new[] { 9 });
        Assert.Equal(new[] { "Mill" }, preview.BuildingNames);
        Assert.Equal(new[] { 9 }, preview.SlotIds);
    }

    [Fact]
    public void Http_get_state_slots_includes_preview_fields_in_day()
    {
        var world = new PreviewWorld
        {
            HintsValue = new WorldHints
            {
                SceneName = "Nordfels",
                SceneState = "InGame",
                Timestate = "Day",
                MatchState = "InMatch"
            },
            Template =
            {
                Clock = new ClockDto { Timestate = "Day" },
                Slots = new List<SlotDto>
                {
                    new()
                    {
                        BuildingName = "House",
                        NextUpgradeOrBuildCost = 2,
                        NextUpgradeIsChoice = false,
                        Tooltip = "Upgrade House",
                        NextUpgradeLabel = "House L2",
                        UnlockPreview = new SlotUnlockPreviewDto
                        {
                            BuildingNames = { "Mill" },
                            SlotIds = { 4418 }
                        }
                    }
                }
            }
        };

        var previous = GameFacade.Current;
        GameFacade.Current = new GameFacade(world);
        try
        {
            var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/state/slots"));
            Assert.Equal(200, res.Status);
            var dto = Json.Deserialize<StateDto>(res.Body);
            Assert.Equal(Phases.Day, dto!.Phase);
            Assert.NotNull(dto.Slots);
            Assert.Single(dto.Slots!);
            Assert.Equal("Upgrade House", dto.Slots![0].Tooltip);
            Assert.Equal("House L2", dto.Slots[0].NextUpgradeLabel);
            Assert.Equal(new[] { "Mill" }, dto.Slots[0].UnlockPreview.BuildingNames);
            Assert.Equal(new[] { 4418 }, dto.Slots[0].UnlockPreview.SlotIds);
            Assert.Contains("\"tooltip\"", res.Body);
            Assert.Contains("\"nextUpgradeLabel\"", res.Body);
            Assert.Contains("\"unlockPreview\"", res.Body);
        }
        finally
        {
            GameFacade.Current = previous;
        }
    }

    sealed class FakeUnlockSlot
    {
        public string buildingName = "Mill";
    }

    sealed class FakeBuildSlot
    {
        public string buildingName = "House";
        public int Level => 1;
        public bool NextUpgradeIsChoice => false;
        public FakeUnlockSlot Unlock { get; } = new();

        public string ReturnTooltip() => "full tooltip";

        public string GET_LOCIDENTIFIER_UPGRADE(int level) => "Building/House Upgrade " + level;

        public List<FakeUnlockSlot> GetBuildSlotsThatWillUnlockWhenUpgraded() => new() { Unlock };
    }

    sealed class FakeChoice
    {
        public string name;

        public FakeChoice(string name) => this.name = name;
    }

    sealed class FakeBranch
    {
        public FakeChoice choiceDetails;

        public FakeBranch(string name) => choiceDetails = new FakeChoice(name);
    }

    sealed class FakeUpgrade
    {
        public List<FakeBranch> upgradeBranches = new()
        {
            new FakeBranch("Archers"),
            new FakeBranch("Knights")
        };
    }

    sealed class FakeChoiceSlot
    {
        public int Level => 0;
        public bool NextUpgradeIsChoice => true;
        public List<FakeUpgrade> Upgrades { get; } = new() { new FakeUpgrade() };

        public string ReturnTooltip() => "";

        public string GET_LOCIDENTIFIER_CHOICENAME(FakeChoice choice) =>
            "Building/Tower Choice " + choice.name;

        public string GET_LOCIDENTIFIER_CHOICEDESCRIPTION(FakeChoice choice) =>
            "Building/Tower Choice " + choice.name + " Description";

        public List<object> GetBuildSlotsThatWillUnlockWhenUpgraded() => new();
    }

    sealed class PreviewWorld : IWorld
    {
        public WorldHints HintsValue { get; set; } = new();
        public StateDto Template { get; set; } = new();

        public WorldHints Hints() => HintsValue;

        public void Capture(GameFacade facade, StateDto dto, StateInclude include)
        {
            dto.Clock = Template.Clock;
            dto.Slots = Template.Slots;
            _ = include;
            _ = facade;
        }
    }
}
