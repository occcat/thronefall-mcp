using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Tests.GameFakes;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class LoadoutSignatureTests : IDisposable
{
    public LoadoutSignatureTests()
    {
        Loadout.Reset();
        RuntimeState.Reset();
        LoadoutUIHelper.TrySelectCalls = 0;
        PerkSelectionGroup.StringSelects = 0;
        PerkSelectionGroup.ItemSelects = 0;
        LevelInteractor.ZeroArgBegins = 0;
        LevelInteractor.PlayerBegins = 0;
        LevelSelectManager.LoadSceneCalls = 0;
        LevelSelectManager.instance = new LevelSelectManager();
        PlayerInteraction.instance = new PlayerInteraction();
    }

    public void Dispose()
    {
        Loadout.Reset();
        RuntimeState.Reset();
    }

    [Fact]
    public void Select_picks_live_tfui_equippable_and_ignores_void_helper()
    {
        var mint = new TFUIEquippable
        {
            equippableData = { displayName = "Royal Mint" },
            isPerk = true
        };
        var helper = new LoadoutUIHelper();
        helper.perks.Add(mint);
        BindLoadout(helper);
        Loadout.Runtime = Loadout.ReflectionRuntime.Instance;

        var result = Loadout.Select("Royal Mint", "perk");

        Assert.True(result.Ok);
        Assert.True(result.Selected);
        Assert.Equal(1, mint.PickCalls);
        Assert.Equal(0, LoadoutUIHelper.TrySelectCalls);
        Assert.Equal(0, PerkSelectionGroup.StringSelects);
    }

    [Fact]
    public void Select_does_not_treat_void_helper_as_success()
    {
        var helper = new LoadoutUIHelper();
        BindLoadout(helper);
        Loadout.Runtime = Loadout.ReflectionRuntime.Instance;

        var result = Loadout.Select("Royal Mint", "perk");

        Assert.False(result.Ok);
        Assert.Equal(0, LoadoutUIHelper.TrySelectCalls);
        Assert.Equal(ErrorCodes.NotFound, result.Error);
    }

    [Fact]
    public void Select_honors_locked_and_does_not_pick()
    {
        var mint = new TFUIEquippable
        {
            equippableData = { displayName = "Royal Mint", IsUnlocked = false },
            locked = true,
            isPerk = true
        };
        var helper = new LoadoutUIHelper();
        helper.perks.Add(mint);
        BindLoadout(helper);
        Loadout.Runtime = Loadout.ReflectionRuntime.Instance;

        var result = Loadout.Select("Royal Mint", "perk");

        Assert.False(result.Ok);
        Assert.Equal(0, mint.PickCalls);
        Assert.Equal(0, LoadoutUIHelper.TrySelectCalls);
    }

    [Fact]
    public void Select_perk_item_uses_item_not_string()
    {
        var item = new PerkSelectionItem
        {
            equippable = { displayName = "Royal Mint" }
        };
        var group = new PerkSelectionGroup();
        item.perkSelectionGroup = group;
        BindLoadout(new LoadoutUIHelper(), perkItems: new object[] { item }, perkGroups: new object[] { group });
        Loadout.Runtime = Loadout.ReflectionRuntime.Instance;

        var result = Loadout.Select("Royal Mint", "perk");

        Assert.True(result.Ok);
        Assert.Equal(1, PerkSelectionGroup.ItemSelects);
        Assert.Equal(0, PerkSelectionGroup.StringSelects);
        Assert.Equal(0, LoadoutUIHelper.TrySelectCalls);
    }

    [Fact]
    public void Start_level_calls_interaction_begin_with_player_then_play()
    {
        var nordfels = new LevelInteractor
        {
            levelInfo = { sceneName = "Nordfels" }
        };
        LevelSelectManager.instance.levelInteractors.Add(nordfels);
        BindLoadout(new LoadoutUIHelper());
        Loadout.Runtime = Loadout.ReflectionRuntime.Instance;

        var result = Loadout.StartLevel("Nordfels");

        Assert.True(result.Ok);
        Assert.Equal(1, LevelInteractor.PlayerBegins);
        Assert.Equal(0, LevelInteractor.ZeroArgBegins);
        Assert.Same(PlayerInteraction.instance, nordfels.LastPlayer);
        Assert.Equal(1, LevelSelectManager.instance.PlayPressed);
        Assert.Equal(0, LevelSelectManager.LoadSceneCalls);
    }

    [Fact]
    public void Http_start_level_does_not_use_zero_arg_begin()
    {
        RuntimeState.Phase = Phases.LevelSelect;
        var nordfels = new LevelInteractor { levelInfo = { sceneName = "Nordfels" } };
        LevelSelectManager.instance.levelInteractors.Add(nordfels);
        BindLoadout(new LoadoutUIHelper());
        Loadout.Runtime = Loadout.ReflectionRuntime.Instance;

        var res = Router.CreateDefault().Dispatch(RequestContext.Create(
            "POST",
            "/level/start",
            body: "{\"sceneName\":\"Nordfels\"}"));

        Assert.Equal(200, res.Status);
        Assert.Equal(1, LevelInteractor.PlayerBegins);
        Assert.Equal(0, LevelInteractor.ZeroArgBegins);
        Assert.Equal(0, LevelSelectManager.LoadSceneCalls);
    }

    static void BindLoadout(
        LoadoutUIHelper helper,
        object[]? perkItems = null,
        object[]? perkGroups = null)
    {
        GameReflection.Types = name => name switch
        {
            "LoadoutUIHelper" => typeof(LoadoutUIHelper),
            "TFUIEquippable" => typeof(TFUIEquippable),
            "PerkSelectionGroup" => typeof(PerkSelectionGroup),
            "PerkSelectionItem" => typeof(PerkSelectionItem),
            "LevelInteractor" => typeof(LevelInteractor),
            "LevelSelectManager" => typeof(LevelSelectManager),
            "PlayerInteraction" => typeof(PlayerInteraction),
            "Equippable" => typeof(Equippable),
            _ => null
        };
        GameReflection.LiveObjects = t =>
        {
            if (t == typeof(LoadoutUIHelper))
                return new object[] { helper };
            if (t == typeof(TFUIEquippable))
            {
                var all = new List<object>();
                all.AddRange(helper.perks);
                all.AddRange(helper.weapons);
                all.AddRange(helper.mutators);
                return all.ToArray();
            }

            if (t == typeof(PerkSelectionItem))
                return perkItems ?? Array.Empty<object>();
            if (t == typeof(PerkSelectionGroup))
                return perkGroups ?? Array.Empty<object>();
            if (t == typeof(LevelInteractor))
                return LevelSelectManager.instance.levelInteractors.ToArray();
            return Array.Empty<object>();
        };
    }
}