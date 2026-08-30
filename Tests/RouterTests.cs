using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Http.Modules;
using Xunit;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class RouterTests
{
    [Fact]
    public void Health_alive_returns_ok_without_main_thread()
    {
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("GET", "/health"));
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<HealthResponse>(res.Body);
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.Equal("ThronefallControl", body.Plugin);
        Assert.Equal(PluginInfo.Version, body.Version);
        Assert.False(body.CheatsEnabled);
        Assert.False(body.Ready);
    }

    [Fact]
    public void Health_alive_does_not_enqueue_main_thread()
    {
        var previous = MainThread.Current;
        var mt = new MainThread(TimeSpan.FromSeconds(2));
        MainThread.Current = mt;
        try
        {
            var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/health"));
            Assert.Equal(200, res.Status);
            Assert.Equal(0, mt.QueueDepth);
        }
        finally
        {
            MainThread.Current = previous;
        }
    }

    [Fact]
    public async Task Health_ready_reads_frame_on_main_thread()
    {
        var previousMt = MainThread.Current;
        var previousReader = HealthModule.FrameCountReader;
        var mt = new MainThread(TimeSpan.FromSeconds(2));
        MainThread.Current = mt;
        HealthModule.FrameCountReader = () => 17;
        using var cts = new CancellationTokenSource();
        var pump = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                mt.Pump();
                try
                {
                    await Task.Delay(5, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        });

        try
        {
            var res = Router.CreateDefault().Dispatch(RequestContext.Create("GET", "/health/ready"));
            Assert.Equal(200, res.Status);
            var body = Json.Deserialize<HealthResponse>(res.Body);
            Assert.NotNull(body);
            Assert.True(body!.Ok);
            Assert.True(body.Ready);
            Assert.Equal(17, body.FrameCount);
        }
        finally
        {
            cts.Cancel();
            try { await pump; } catch (OperationCanceledException) { }
            MainThread.Current = previousMt;
            HealthModule.FrameCountReader = previousReader;
        }
    }

    [Fact]
    public void State_module_is_discovered()
    {
        var previous = GameFacade.Current;
        GameFacade.Current = new GameFacade(new EmptyWorld());
        try
        {
            var router = Router.CreateDefault();
            var res = router.Dispatch(RequestContext.Create("GET", "/state"));
            Assert.Equal(200, res.Status);
            var body = Json.Deserialize<StateDto>(res.Body);
            Assert.True(body!.Ok);
            Assert.False(string.IsNullOrEmpty(body.Phase));
        }
        finally
        {
            GameFacade.Current = previous;
        }
    }

    [Fact]
    public void Unknown_route_is_not_found()
    {
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("POST", "/no-such-endpoint"));
        Assert.Equal(404, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.NotNull(err);
        Assert.False(err!.Ok);
        Assert.Equal(ErrorCodes.NotFound, err.Error);
    }

    [Fact]
    public void Path_parameters_are_captured()
    {
        var router = new Router();
        router.Map("POST", "/slots/{id}/build", ctx => Json.Ok(new { id = ctx.RouteValues["id"] }));
        var res = router.Dispatch(RequestContext.Create("POST", "/slots/4412/build"));
        Assert.Equal(200, res.Status);
        Assert.Contains("4412", res.Body);
    }

    [Fact]
    public void DryRun_query_is_parsed()
    {
        var ctx = RequestContext.Create("POST", "/slots/1/build?dryRun=true");
        Assert.True(ctx.DryRun);
        Assert.False(RequestContext.Create("POST", "/slots/1/build").DryRun);
    }

    sealed class ProbeModule : IRouteModule
    {
        public void Register(Router router) =>
            router.Map("GET", "/probe", _ => Json.Ok(new { ok = true, probe = true }));
    }

    [Fact]
    public void Modules_register_without_editing_router()
    {
        var router = new Router();
        router.AddModule(new ProbeModule());
        var res = router.Dispatch(RequestContext.Create("GET", "/probe"));
        Assert.Equal(200, res.Status);
        Assert.Contains("probe", res.Body);
    }

    sealed class EmptyWorld : IWorld
    {
        public WorldHints Hints() => new() { SceneState = "MainMenu", SceneName = "_StartMenu" };

        public void Capture(GameFacade facade, StateDto dto, StateInclude include)
        {
            _ = facade;
            _ = include;
            dto.Level = new LevelDto { SceneName = "_StartMenu" };
        }
    }
}
