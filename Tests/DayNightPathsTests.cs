using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Http.Modules;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class DayNightPathsTests
{
    [Fact]
    public void Default_router_registers_night_and_path()
    {
        var router = Router.CreateDefault();
        var night = router.Dispatch(RequestContext.Create("POST", "/night/call", body: "{}"));
        Assert.DoesNotContain("no route", night.Body, StringComparison.OrdinalIgnoreCase);
        var path = router.Dispatch(RequestContext.Create("POST", "/path/toggle", body: "{\"id\":{\"instanceId\":1,\"generation\":0}}"));
        Assert.DoesNotContain("no route", path.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Call_night_when_free_on_day()
    {
        using var session = Session.Day(free: true);
        var res = session.Dispatch("POST", "/night/call", "{}");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<CallNightResponse>(res.Body);
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.True(body.Called);
        Assert.Equal("night", body.Phase);
        Assert.Equal(1, session.World.SwitchToNightCalls);
        Assert.Equal(0, session.World.SkipWaveCalls);
    }

    [Fact]
    public void Call_night_when_not_free()
    {
        using var session = Session.Day(free: false);
        var res = session.Dispatch("POST", "/night/call", "{\"clientRequestId\":\"n-1\"}");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<CallNightResponse>(res.Body);
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.True(body.Called);
        Assert.Equal("night", body.Phase);
        Assert.Equal(1, session.World.SwitchToNightCalls);
    }

    [Fact]
    public void Call_night_illegal_when_already_night()
    {
        using var session = Session.Night();
        var res = session.Dispatch("POST", "/night/call", "{}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body)!;
        Assert.Equal(ErrorCodes.IllegalPhase, err.Error);
        Assert.Equal("night", err.Phase);
        Assert.Equal(0, session.World.SwitchToNightCalls);
    }

    [Fact]
    public void Call_night_transition_in_progress()
    {
        using var session = Session.Day(free: true);
        session.World.SceneTransitionIsRunning = true;
        var res = session.Dispatch("POST", "/night/call", "{}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body)!;
        Assert.Equal(ErrorCodes.TransitionInProgress, err.Error);
        Assert.Equal("transition", err.Phase);
        Assert.Equal(session.Ids.SceneGeneration, err.Generation);
        Assert.Equal(0, session.World.SwitchToNightCalls);
    }

    [Fact]
    public void Call_night_dry_run_does_not_switch()
    {
        using var session = Session.Day(free: true);
        var res = session.Dispatch("POST", "/night/call?dryRun=true", "{\"clientRequestId\":\"n-dry\"}");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<DryRunResponse>(res.Body);
        Assert.NotNull(body);
        Assert.True(body!.Ok);
        Assert.True(body.DryRun);
        Assert.Equal("call_night", body.Would.Action);
        Assert.False(body.Would.Blocked);
        Assert.Equal(0, session.World.SwitchToNightCalls);
        Assert.Equal("day", session.World.Phase);
    }

    [Fact]
    public void Path_toggle_opens_cutter_on_day()
    {
        using var session = Session.Day(free: true);
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"clientRequestId\":\"p-1\",\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<TogglePathResponse>(res.Body)!;
        Assert.True(body.Ok);
        Assert.True(body.PathOpened);
        Assert.Equal(8, body.ToggleCost);
        Assert.Equal(1, cutter.ToggleCalls);
        Assert.Equal(1, cutter.PathStateChangedCalls);
        Assert.Equal(0, cutter.ToggleCompleteCalls);
        Assert.Equal(92, session.World.Balance);
        Assert.Equal(1, session.World.SpendCalls);
        Assert.Equal(8, session.World.SpentTotal);
    }

    [Fact]
    public void Path_toggle_spends_toggle_cost()
    {
        using var session = Session.Day(free: true);
        session.World.Balance = 40;
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(200, res.Status);
        Assert.Equal(1, cutter.ToggleCalls);
        Assert.Equal(32, session.World.Balance);
        Assert.Equal(1, session.World.SpendCalls);
        Assert.Equal(8, session.World.SpentTotal);
    }

    [Fact]
    public void Path_toggle_zero_cost_does_not_spend()
    {
        using var session = Session.Day(free: true);
        session.World.Balance = 40;
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 0);
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(200, res.Status);
        Assert.Equal(1, cutter.ToggleCalls);
        Assert.Equal(40, session.World.Balance);
        Assert.Equal(0, session.World.SpendCalls);
        Assert.Equal(0, session.World.SpentTotal);
    }

    [Fact]
    public void Path_toggle_uses_toggle_complete_when_event_unbound()
    {
        using var session = Session.Day(free: true);
        session.World.Balance = 20;
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        cutter.HasBoundPathStateChanged = false;
        cutter.HasToggleComplete = true;
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(200, res.Status);
        Assert.Equal(0, cutter.ToggleCalls);
        Assert.Equal(1, cutter.ToggleCompleteCalls);
        Assert.Equal(0, cutter.PathStateChangedCalls);
        Assert.Equal(12, session.World.Balance);
        Assert.Equal(8, session.World.SpentTotal);
    }

    [Fact]
    public void Path_toggle_respects_toogleOnlyAtDay()
    {
        using var session = Session.Night();
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body)!;
        Assert.Equal(ErrorCodes.IllegalPhase, err.Error);
        Assert.Equal("night", err.Phase);
        Assert.Contains("toogleOnlyAtDay", err.Message);
        Assert.Equal(0, cutter.ToggleCalls);
        Assert.False(cutter.PathOpened);
        Assert.Equal(0, session.World.SpendCalls);
    }

    [Fact]
    public void Path_toggle_allows_night_when_not_day_only()
    {
        using var session = Session.Night();
        var cutter = session.AddCutter(88, toogleOnlyAtDay: false, cost: 5);
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":88,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(200, res.Status);
        Assert.Equal(1, cutter.ToggleCalls);
        Assert.True(cutter.PathOpened);
        Assert.Equal(95, session.World.Balance);
        Assert.Equal(1, session.World.SpendCalls);
        Assert.Equal(5, session.World.SpentTotal);
    }

    [Fact]
    public void Path_toggle_transition_in_progress()
    {
        using var session = Session.Day(free: true);
        session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        session.World.SceneTransitionIsRunning = true;
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body)!;
        Assert.Equal(ErrorCodes.TransitionInProgress, err.Error);
        Assert.Equal(session.Ids.SceneGeneration, err.Generation);
    }

    [Fact]
    public void Path_toggle_dry_run_does_not_mutate()
    {
        using var session = Session.Day(free: true);
        session.World.Balance = 20;
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        var res = session.Dispatch(
            "POST",
            "/path/toggle?dryRun=true",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<DryRunResponse>(res.Body)!;
        Assert.True(body.DryRun);
        Assert.Equal("toggle_path", body.Would.Action);
        Assert.Equal(8, body.Would.Cost);
        Assert.Equal(12, body.Would.BalanceAfter);
        Assert.False(body.Would.Blocked);
        Assert.Equal(0, cutter.ToggleCalls);
        Assert.False(cutter.PathOpened);
        Assert.Equal(20, session.World.Balance);
        Assert.Equal(0, session.World.SpendCalls);
    }

    [Fact]
    public void Path_toggle_stale_id()
    {
        using var session = Session.Day(free: true);
        session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        var oldGen = session.Ids.SceneGeneration;
        session.Ids.BeginScene();
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + oldGen + "}}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body)!;
        Assert.Equal(ErrorCodes.StaleId, err.Error);
    }

    [Fact]
    public void Path_toggle_insufficient_gold()
    {
        using var session = Session.Day(free: true);
        session.World.Balance = 3;
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body)!;
        Assert.Equal(ErrorCodes.InsufficientGold, err.Error);
        Assert.Equal(0, cutter.ToggleCalls);
        Assert.Equal(3, session.World.Balance);
        Assert.Equal(0, session.World.SpendCalls);
    }

    [Fact]
    public void Path_toggle_invalid_use_has_clear_message()
    {
        using var session = Session.Day(free: true);
        session.World.Balance = 50;
        var cutter = session.AddCutter(77, toogleOnlyAtDay: true, cost: 8);
        cutter.IsToggleValidToUse = false;
        var res = session.Dispatch(
            "POST",
            "/path/toggle",
            "{\"id\":{\"instanceId\":77,\"generation\":" + session.Ids.SceneGeneration + "}}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body)!;
        Assert.Equal(ErrorCodes.IllegalPhase, err.Error);
        Assert.Contains("IsToggleValidToUse", err.Message);
        Assert.DoesNotContain("illegal in phase", err.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, cutter.ToggleCalls);
        Assert.Equal(0, session.World.SpendCalls);
        Assert.Equal(50, session.World.Balance);
    }

    [Fact]
    public void Invalid_json_is_400()
    {
        using var session = Session.Day(free: true);
        var night = session.Dispatch("POST", "/night/call", "{not-json");
        Assert.Equal(400, night.Status);
        var nightErr = Json.Deserialize<ErrorResponse>(night.Body)!;
        Assert.Equal("invalid_json", nightErr.Error);
        Assert.NotEqual(ErrorCodes.UnityException, nightErr.Error);
        Assert.Equal(0, session.World.SwitchToNightCalls);

        var path = session.Dispatch("POST", "/path/toggle", "{also-bad");
        Assert.Equal(400, path.Status);
        var pathErr = Json.Deserialize<ErrorResponse>(path.Body)!;
        Assert.Equal("invalid_json", pathErr.Error);
        Assert.Equal(0, session.World.SpendCalls);
    }

    [Fact]
    public void Timeout_does_not_read_phase_on_http_thread()
    {
        using var session = Session.Day(free: true);
        session.World.PhaseAllowedThreadId = 0;
        var mt = new MainThread(TimeSpan.FromMilliseconds(40));
        var prev = MainThread.Current;
        MainThread.Current = mt;
        try
        {
            var res = session.Dispatch("POST", "/night/call", "{}");
            Assert.Equal(504, res.Status);
            var err = Json.Deserialize<ErrorResponse>(res.Body)!;
            Assert.Equal(ErrorCodes.MainThreadTimeout, err.Error);
            Assert.Null(err.Phase);
            Assert.Equal(0, session.World.SwitchToNightCalls);
        }
        finally
        {
            MainThread.Current = prev;
        }
    }

    [Fact]
    public async Task Call_night_uses_main_thread_queue()
    {
        using var session = Session.Day(free: true);
        var mt = new MainThread(TimeSpan.FromSeconds(2));
        var prev = MainThread.Current;
        MainThread.Current = mt;
        using var cts = new CancellationTokenSource();
        var dispatcher = Task.Run(async () =>
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
            var res = session.Dispatch("POST", "/night/call", "{}");
            Assert.Equal(200, res.Status);
            Assert.Equal(1, session.World.SwitchToNightCalls);
        }
        finally
        {
            MainThread.Current = prev;
            cts.Cancel();
            try { await dispatcher; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public void ReflectionCache_try_init_does_not_throw_without_game()
    {
        ReflectionCache.TryInit();
        Assert.Null(ReflectionCache.ToggleCutPath);
        Assert.Null(ReflectionCache.DayNightCycleSwitchToNight);
    }

    sealed class Session : IDisposable
    {
        readonly GameFacade _previousFacade;
        readonly Router _router;

        public Session(FakeWorld world)
        {
            World = world;
            Ids = new IdRegistry();
            Ids.BeginScene();
            _previousFacade = GameFacade.Current;
            GameFacade.Current = new GameFacade { Ids = Ids, World = world };
            _router = new Router();
            _router.AddModule(new DayNightModule());
            _router.AddModule(new PathsModule());
        }

        public FakeWorld World { get; }
        public IdRegistry Ids { get; }

        public static Session Day(bool free) =>
            new(new FakeWorld { Phase = "day", IsFreeToCallNight = free, Balance = 100 });

        public static Session Night() =>
            new(new FakeWorld { Phase = "night", IsFreeToCallNight = false, Balance = 100 });

        public FakeCutter AddCutter(int instanceId, bool toogleOnlyAtDay, int cost)
        {
            var cutter = new FakeCutter
            {
                InstanceId = instanceId,
                Name = "Cut Path North",
                ToggleCost = cost,
                ToogleOnlyAtDay = toogleOnlyAtDay,
                CanBeInteractedWith = true,
                IsToggleValidToUse = true
            };
            Ids.Register(instanceId, "cutter", cutter.Name, cutter);
            return cutter;
        }

        public HttpResponse Dispatch(string method, string url, string body) =>
            _router.Dispatch(RequestContext.Create(method, url, body: body));

        public void Dispose()
        {
            GameFacade.Current = _previousFacade;
        }
    }

    sealed class FakeWorld : IGameWorld
    {
        string _phase = "day";

        public string Phase
        {
            get
            {
                if (PhaseAllowedThreadId is int allowed
                    && Thread.CurrentThread.ManagedThreadId != allowed)
                {
                    throw new InvalidOperationException("World.Phase read off the main thread");
                }

                return _phase;
            }
            set => _phase = value;
        }

        public int? PhaseAllowedThreadId { get; set; }
        public bool SceneTransitionIsRunning { get; set; }
        public bool IsFreeToCallNight { get; set; } = true;
        public int Balance { get; set; } = 100;
        public bool SwitchToNightSupported { get; set; } = true;
        public bool SpendCoinsSupported { get; set; } = true;
        public int SwitchToNightCalls { get; private set; }
        public int SkipWaveCalls { get; set; }
        public int SpendCalls { get; private set; }
        public int SpentTotal { get; private set; }

        public void SwitchToNight()
        {
            SwitchToNightCalls++;
            _phase = "night";
            IsFreeToCallNight = false;
        }

        public void SpendCoins(int amount)
        {
            SpendCalls++;
            SpentTotal += amount;
            Balance -= amount;
        }
    }

    sealed class FakeCutter : IPathCutter
    {
        public int InstanceId { get; set; }
        public string Name { get; set; } = "";
        public bool PathOpened { get; set; }
        public int ToggleCost { get; set; }
        public bool ToogleOnlyAtDay { get; set; }
        public bool ToggleOnlyAtNight { get; set; }
        public bool CanBeInteractedWith { get; set; } = true;
        public bool IsToggleValidToUse { get; set; } = true;
        public bool ToggleSupported { get; set; } = true;
        public bool HasBoundPathStateChanged { get; set; } = true;
        public bool HasToggleComplete { get; set; }
        public int ToggleCalls { get; private set; }
        public int PathStateChangedCalls { get; private set; }
        public int ToggleCompleteCalls { get; private set; }

        public void ToggleCutPath()
        {
            ToggleCalls++;
            PathOpened = !PathOpened;
        }

        public void InvokePathStateChanged()
        {
            PathStateChangedCalls++;
        }

        public void ToggleComplete()
        {
            ToggleCompleteCalls++;
            PathOpened = !PathOpened;
        }
    }
}
