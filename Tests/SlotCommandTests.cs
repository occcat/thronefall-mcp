using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Http.Modules;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class SlotCommandTests
{
    [Fact]
    public void Harvest_module_is_registered()
    {
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("POST", "/harvest", body: "{}"));
        Assert.NotEqual(404, res.Status);
    }

    [Fact]
    public async Task Harvest_dry_run_does_not_mutate()
    {
        await using var env = Env();
        env.Backend.Balance = 10;
        env.Backend.Add(new MemorySlot
        {
            InstanceId = 1,
            BuildingName = "House",
            CanBeHarvested = true,
            GoldIncome = 3
        });

        var res = await env.Post("/harvest?dryRun=true", """{"clientRequestId":"h-dry"}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<HarvestResponse>(res.Body);
        Assert.True(body!.DryRun);
        Assert.Equal(1, body.Harvested);
        Assert.Equal(3, body.GoldGained);
        Assert.Equal(10, env.Backend.Balance);
        Assert.False(env.Backend.List()[0].HarvestedToday);
        Assert.True(env.Backend.List()[0].CanBeHarvested);
    }

    [Fact]
    public async Task Harvest_all_adds_gold_once()
    {
        await using var env = Env();
        env.Backend.Balance = 4;
        env.Backend.Add(new MemorySlot { InstanceId = 1, BuildingName = "House", CanBeHarvested = true, GoldIncome = 2 });
        env.Backend.Add(new MemorySlot { InstanceId = 2, BuildingName = "Mill", CanBeHarvested = true, GoldIncome = 3 });

        var res = await env.Post("/harvest", """{"clientRequestId":"h-1"}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<HarvestResponse>(res.Body);
        Assert.Equal(2, body!.Harvested);
        Assert.Equal(5, body.GoldGained);
        Assert.Equal(9, env.Backend.Balance);
        Assert.True(env.Backend.List()[0].HarvestedToday);
    }

    [Fact]
    public async Task Harvest_at_night_is_illegal_phase()
    {
        await using var env = Env();
        env.Backend.Phase = "night";
        env.Backend.Add(new MemorySlot { InstanceId = 1, CanBeHarvested = true, GoldIncome = 2 });

        var res = await env.Post("/harvest", """{"clientRequestId":"h-night"}""");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
        Assert.Equal("night", err.Phase);
        Assert.False(env.Backend.List()[0].HarvestedToday);
    }

    [Fact]
    public async Task Build_dry_run_does_not_mutate()
    {
        await using var env = Env();
        env.Backend.Balance = 20;
        env.Backend.Add(new MemorySlot
        {
            InstanceId = 4412,
            BuildingName = "House",
            CanBeUpgraded = true,
            NextUpgradeOrBuildCost = 2,
            Level = 0
        });

        var res = await env.Post("/slots/4412/build?dryRun=true", """{"clientRequestId":"b-dry","generation":1}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<DryRunResponse>(res.Body);
        Assert.True(body!.DryRun);
        Assert.Equal("build", body.Would.Action);
        Assert.Equal("House", body.Would.Slot);
        Assert.Equal(2, body.Would.Cost);
        Assert.Equal(18, body.Would.BalanceAfter);
        Assert.False(body.Would.Blocked);
        Assert.Equal(20, env.Backend.Balance);
        Assert.Equal(0, env.Backend.List()[0].Level);
    }

    [Fact]
    public async Task Build_pays_and_increments_level()
    {
        await using var env = Env();
        env.Backend.Balance = 20;
        env.Backend.Add(new MemorySlot
        {
            InstanceId = 4412,
            BuildingName = "House",
            CanBeUpgraded = true,
            NextUpgradeOrBuildCost = 2,
            Level = 0
        });

        var res = await env.Post("/slots/4412/build", """{"clientRequestId":"b-1","generation":1}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<SlotMutateResponse>(res.Body);
        Assert.True(body!.Applied);
        Assert.False(body.NeedsChoice);
        Assert.Equal(1, body.Level);
        Assert.Equal(18, env.Backend.Balance);
    }

    [Fact]
    public async Task Upgrade_at_night_is_illegal_phase()
    {
        await using var env = Env();
        env.Backend.Phase = "night";
        env.Backend.Balance = 20;
        env.Backend.Add(new MemorySlot { InstanceId = 7, CanBeUpgraded = true, NextUpgradeOrBuildCost = 1 });

        var res = await env.Post("/slots/7/upgrade", """{"clientRequestId":"u-night"}""");
        Assert.Equal(409, res.Status);
        Assert.Equal(ErrorCodes.IllegalPhase, Json.Deserialize<ErrorResponse>(res.Body)!.Error);
        Assert.Equal(20, env.Backend.Balance);
    }

    [Fact]
    public async Task Build_stale_id_does_not_mutate()
    {
        await using var env = Env();
        env.Backend.Balance = 20;
        env.Backend.Add(new MemorySlot { InstanceId = 9, BuildingName = "House", CanBeUpgraded = true, NextUpgradeOrBuildCost = 2 });
        var oldGen = env.Backend.Generation;
        env.Backend.AdvanceScene();
        env.Backend.Add(new MemorySlot { InstanceId = 9, BuildingName = "House", CanBeUpgraded = true, NextUpgradeOrBuildCost = 2 });

        var res = await env.Post("/slots/9/build", $"{{\"clientRequestId\":\"b-stale\",\"generation\":{oldGen}}}");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.StaleId, err!.Error);
        Assert.Equal(20, env.Backend.Balance);
        Assert.Equal(0, env.Backend.List()[0].Level);
    }

    [Fact]
    public async Task Build_insufficient_gold()
    {
        await using var env = Env();
        env.Backend.Balance = 1;
        env.Backend.Add(new MemorySlot
        {
            InstanceId = 3,
            BuildingName = "Tower",
            CanBeUpgraded = true,
            NextUpgradeOrBuildCost = 5,
            Level = 1
        });

        var res = await env.Post("/slots/3/upgrade", """{"clientRequestId":"u-poor"}""");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.InsufficientGold, err!.Error);
        Assert.Equal(1, env.Backend.Balance);
        Assert.Equal(1, env.Backend.List()[0].Level);
    }

    [Fact]
    public async Task Build_choice_slot_does_not_pick_a_branch()
    {
        await using var env = Env();
        env.Backend.Balance = 10;
        env.Backend.Add(ChoiceSlot(5, delay: 0));

        var res = await env.Post("/slots/5/build", """{"clientRequestId":"b-choice"}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<SlotMutateResponse>(res.Body);
        Assert.True(body!.NeedsChoice);
        Assert.True(body.IsWaitingForChoice);
        Assert.Equal(2, body.Choices.Count);
        Assert.Equal(0, body.Level);
        Assert.Equal(6, env.Backend.Balance);
        Assert.Null(env.Backend.Get(5).SelectedChoice);
    }

    [Fact]
    public async Task Choice_required_when_nothing_pending()
    {
        await using var env = Env();
        env.Backend.Add(new MemorySlot { InstanceId = 4, BuildingName = "House", CanBeUpgraded = true });

        var res = await env.Post("/slots/4/choice", """{"clientRequestId":"c-none","name":"Archers"}""");
        Assert.Equal(409, res.Status);
        Assert.Equal(ErrorCodes.ChoiceRequired, Json.Deserialize<ErrorResponse>(res.Body)!.Error);
    }

    [Fact]
    public async Task Choice_applies_named_branch()
    {
        await using var env = Env();
        env.Backend.Balance = 10;
        env.Backend.Add(ChoiceSlot(5, delay: 0));
        await env.Post("/slots/5/build", """{"clientRequestId":"b-then-c"}""");

        var res = await env.Post("/slots/5/choice", """{"clientRequestId":"c-1","name":"Archers"}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<SlotMutateResponse>(res.Body);
        Assert.True(body!.Applied);
        Assert.False(body.NeedsChoice);
        Assert.Equal(1, body.Level);
        Assert.Equal("Archers", env.Backend.Get(5).SelectedChoice);
    }

    [Fact]
    public async Task Cancel_choice_in_day_clears_busy()
    {
        await using var env = Env();
        env.Backend.Balance = 10;
        env.Backend.Add(ChoiceSlot(5, delay: 0));
        await env.Post("/slots/5/build", """{"clientRequestId":"b-then-cancel"}""");
        Assert.True(env.Backend.ChoiceBusy);

        var res = await env.Post("/slots/choice/cancel", """{"clientRequestId":"x-1"}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<CancelChoiceResponse>(res.Body);
        Assert.True(body!.Ok);
        Assert.True(body.Canceled);
        Assert.Equal("day", body.Phase);
        Assert.Equal(env.Backend.Generation, body.Generation);
        Assert.False(env.Backend.ChoiceBusy);
        Assert.False(env.Backend.Get(5).IsWaitingForChoice);
        Assert.Null(env.Backend.Get(5).SelectedChoice);
        Assert.Equal(0, env.Backend.Get(5).Level);
        Assert.Equal(1, env.Backend.CancelActiveChoiceCalls);
    }

    [Fact]
    public async Task Cancel_choice_in_menu_is_illegal_phase()
    {
        await using var env = Env();
        env.Backend.Phase = "menu";
        env.Backend.Add(ChoiceSlot(5, delay: 0));
        env.Backend.Get(5).IsWaitingForChoice = true;
        env.Backend.Get(5).PendingChoice = true;

        var res = await env.Post("/slots/choice/cancel", """{"clientRequestId":"x-menu"}""");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.IllegalPhase, err!.Error);
        Assert.Equal("menu", err.Phase);
        Assert.Equal(0, env.Backend.CancelActiveChoiceCalls);
        Assert.True(env.Backend.Get(5).IsWaitingForChoice);
    }

    [Fact]
    public async Task Cancel_choice_without_pending_is_not_found()
    {
        await using var env = Env();
        env.Backend.Add(new MemorySlot { InstanceId = 4, BuildingName = "House", CanBeUpgraded = true });

        var res = await env.Post("/slots/choice/cancel", """{"clientRequestId":"x-none"}""");
        Assert.Equal(409, res.Status);
        var err = Json.Deserialize<ErrorResponse>(res.Body);
        Assert.Equal(ErrorCodes.NotFound, err!.Error);
        Assert.Contains("no upgrade choice to cancel", err.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Json.Deserialize<CancelChoiceResponse>(res.Body)!.Canceled);
        Assert.Equal(0, env.Backend.CancelActiveChoiceCalls);
        Assert.False(env.Backend.ChoiceBusy);
    }

    [Fact]
    public async Task Cancel_choice_dry_run_does_not_call_backend()
    {
        await using var env = Env();
        env.Backend.Balance = 10;
        env.Backend.Add(ChoiceSlot(5, delay: 0));
        await env.Post("/slots/5/build", """{"clientRequestId":"b-then-dry"}""");
        Assert.True(env.Backend.ChoiceBusy);

        var res = await env.Post("/slots/choice/cancel?dryRun=true", """{"clientRequestId":"x-dry"}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<DryRunResponse>(res.Body);
        Assert.True(body!.DryRun);
        Assert.Equal("cancel", body.Would.Action);
        Assert.False(body.Would.Blocked);
        Assert.Equal(0, env.Backend.CancelActiveChoiceCalls);
        Assert.True(env.Backend.ChoiceBusy);
        Assert.True(env.Backend.Get(5).IsWaitingForChoice);
    }

    [Fact]
    public async Task Choice_waits_up_to_four_frames()
    {
        await using var env = Env();
        env.Backend.Balance = 10;
        env.Backend.Add(ChoiceSlot(8, delay: 2));

        var res = await env.Post("/slots/8/build", """{"clientRequestId":"b-wait"}""");
        Assert.Equal(200, res.Status);
        var body = Json.Deserialize<SlotMutateResponse>(res.Body);
        Assert.True(body!.NeedsChoice);
        Assert.True(body.IsWaitingForChoice);
    }

    [Fact]
    public async Task Client_request_id_replays_without_mutating_again()
    {
        await using var env = Env();
        env.Backend.Balance = 4;
        env.Backend.Add(new MemorySlot { InstanceId = 1, BuildingName = "House", CanBeHarvested = true, GoldIncome = 2 });

        var first = await env.Post("/harvest", """{"clientRequestId":"same"}""");
        var second = await env.Post("/harvest", """{"clientRequestId":"same"}""");
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Body, second.Body);
        Assert.Equal(6, env.Backend.Balance);
        Assert.Equal(1, Json.Deserialize<HarvestResponse>(second.Body)!.Harvested);
    }

    [Fact]
    public async Task Transition_rejects_mutate()
    {
        await using var env = Env();
        env.Backend.TransitionInProgress = true;
        env.Backend.Add(new MemorySlot { InstanceId = 1, CanBeHarvested = true, GoldIncome = 1 });
        var res = await env.Post("/harvest", """{"clientRequestId":"h-tr"}""");
        Assert.Equal(409, res.Status);
        Assert.Equal(ErrorCodes.TransitionInProgress, Json.Deserialize<ErrorResponse>(res.Body)!.Error);
    }

    static MemorySlot ChoiceSlot(int id, int delay) =>
        new()
        {
            InstanceId = id,
            BuildingName = "Tower",
            CanBeUpgraded = true,
            NextUpgradeIsChoice = true,
            NextUpgradeOrBuildCost = 4,
            Level = 0,
            ChoiceReadyDelayFrames = delay,
            Choices =
            {
                new ChoiceDto { Name = "Archers", Tooltip = "ranged", CanBePicked = true },
                new ChoiceDto { Name = "Knights", Tooltip = "melee", CanBePicked = true }
            }
        };

    static TestEnv Env()
    {
        var backend = new MemorySlotBackend();
        var cache = new IdempotencyCache();
        var mt = new MainThread(TimeSpan.FromSeconds(2));
        var module = new SlotsModule(backend, cache, mt);
        var router = new Router();
        router.AddModule(module);
        return new TestEnv(backend, router, mt);
    }

    sealed class TestEnv : IAsyncDisposable
    {
        readonly CancellationTokenSource _cts = new();
        readonly Task _pump;

        public TestEnv(MemorySlotBackend backend, Router router, MainThread mainThread)
        {
            Backend = backend;
            Router = router;
            MainThread = mainThread;
            _pump = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    mainThread.Pump();
                    try
                    {
                        await Task.Delay(5, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            });
        }

        public MemorySlotBackend Backend { get; }
        public Router Router { get; }
        public MainThread MainThread { get; }

        public Task<HttpResponse> Post(string path, string body) =>
            Task.Run(() => Router.Dispatch(RequestContext.Create("POST", path, body: body)));

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { await _pump; } catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}
