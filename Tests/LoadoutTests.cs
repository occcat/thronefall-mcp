using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class LoadoutTests : IDisposable
{
    public LoadoutTests()
    {
        RuntimeState.Reset();
        Loadout.Reset();
    }

    public void Dispose()
    {
        RuntimeState.Reset();
        Loadout.Reset();
    }

    [Fact]
    public void Select_in_day_is_illegal_phase()
    {
        RuntimeState.Phase = Phases.Day;
        RuntimeState.Generation = 4;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/loadout/select",
            body: "{\"clientRequestId\":\"l-1\",\"name\":\"Royal Mint\",\"kind\":\"perk\"}"));
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.NotNull(err);
        Assert.False(err!.Ok);
        Assert.Equal(ErrorCodes.IllegalPhase, err.Error);
        Assert.Equal(Phases.Day, err.Phase);
        Assert.Equal(4, err.Generation);
        Assert.Contains("phase=day", err.Message);
    }

    [Fact]
    public void Start_level_in_day_is_illegal_phase()
    {
        RuntimeState.Phase = Phases.Day;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/level/start",
            body: "{\"clientRequestId\":\"s-1\",\"sceneName\":\"Nordfels\"}"));
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
    }

    [Fact]
    public void Select_in_level_select_is_ok()
    {
        RuntimeState.Phase = Phases.LevelSelect;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/loadout/select",
            body: "{\"name\":\"Royal Mint\",\"kind\":\"perk\"}"));
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<LoadoutSelectResult>(res.Body);
        Assert.True(body!.Ok);
        Assert.True(body.Selected);
        Assert.Equal("Royal Mint", body.Name);
    }

    [Fact]
    public void Locked_equippable_is_rejected()
    {
        RuntimeState.Phase = Phases.LevelSelect;
        Loadout.Runtime = new LockedRuntime();
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/loadout/select",
            body: "{\"name\":\"God King\",\"kind\":\"perk\"}"));
        Assert.Equal(404, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.NotFound, err!.Error);
    }

    [Fact]
    public void Start_level_does_not_load_scene_itself()
    {
        RuntimeState.Phase = Phases.LevelSelect;
        var runtime = new RecordingLoadoutRuntime();
        Loadout.Runtime = runtime;
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create(
            "POST",
            "/level/start",
            body: "{\"sceneName\":\"Nordfels\"}"));
        Assert.Equal(200, res.Status);
        Assert.Equal("Nordfels", runtime.StartedScene);
        Assert.False(runtime.LoadedSceneDirectly);
    }

    sealed class LockedRuntime : ILoadoutRuntime
    {
        public bool IsUnlocked(string name, string kind) => false;

        public bool TrySelect(string name, string kind, out string? error)
        {
            error = ErrorCodes.NotFound;
            return false;
        }

        public bool TryStartLevel(string sceneName, out string? error)
        {
            error = null;
            return true;
        }
    }

    sealed class RecordingLoadoutRuntime : ILoadoutRuntime
    {
        public string? StartedScene { get; private set; }
        public bool LoadedSceneDirectly { get; private set; }

        public bool IsUnlocked(string name, string kind) => true;

        public bool TrySelect(string name, string kind, out string? error)
        {
            error = null;
            return true;
        }

        public bool TryStartLevel(string sceneName, out string? error)
        {
            StartedScene = sceneName;
            LoadedSceneDirectly = false;
            error = null;
            return true;
        }
    }
}