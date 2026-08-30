using System;
using System.Collections.Generic;
using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public interface ISlotBackend
{
    string Phase { get; }
    int Generation { get; }
    bool TransitionInProgress { get; }
    int Balance { get; }
    int EnergyCoreBalance { get; }
    bool IsReady { get; }
    bool ChoiceBusy { get; }

    SlotSnapshot? Resolve(int instanceId, int? generation, out string? error);
    IReadOnlyList<SlotSnapshot> List();
    SlotSnapshot Harvest(SlotSnapshot slot);
    SlotSnapshot BuildOrUpgrade(SlotSnapshot slot, bool teleportKingNearby);
    SlotSnapshot CompleteChoice(SlotSnapshot slot, string choiceName, out string? error);
    bool CancelActiveChoice(out string? error);
    SlotSnapshot Refresh(int instanceId);
    void PumpChoiceWait();
}

public sealed class SlotSnapshot
{
    public int InstanceId { get; set; }
    public int Generation { get; set; }
    public string BuildingName { get; set; } = "";
    public int Level { get; set; }
    public int NextUpgradeOrBuildCost { get; set; }
    public int NextUpgradeOrBuildEnergyCoreCost { get; set; }
    public bool CanBeUpgraded { get; set; }
    public bool NextUpgradeIsChoice { get; set; }
    public bool CanBeHarvested { get; set; }
    public bool HarvestedToday { get; set; }
    public bool IsWaitingForChoice { get; set; }
    public bool ChoiceCoroutineRunning { get; set; }
    public int GoldIncome { get; set; }
    public int EnergyCoreIncome { get; set; }
    public Vec3Dto Position { get; set; } = new();
    public List<ChoiceDto> Choices { get; set; } = new();
}

public static class Slots
{
    public const int ChoiceWaitFrames = 4;
    public const string DayPhase = "day";

    public static ISlotBackend? Backend { get; set; }

    public static List<SlotDto> Snapshot(IdRegistry ids)
    {
        try
        {
            return Observation.ReadSlots(ids) ?? new List<SlotDto>();
        }
        catch
        {
            return new List<SlotDto>();
        }
    }

    public static SlotCommandResult Harvest(ISlotBackend backend, int? slotId, int? generation, bool dryRun)
    {
        if (!TryBeginMutate(backend, "/harvest", out var fail))
            return fail!;

        var phase = backend.Phase;
        var gen = backend.Generation;

        if (slotId is int id)
        {
            var slot = backend.Resolve(id, generation, out var error);
            if (slot == null)
                return IdError(error, phase, gen, id);

            if (dryRun)
            {
                var gold = slot.CanBeHarvested ? slot.GoldIncome : 0;
                var cores = slot.CanBeHarvested ? slot.EnergyCoreIncome : 0;
                return SlotCommandResult.Success(new HarvestResponse
                {
                    Ok = true,
                    DryRun = true,
                    Harvested = slot.CanBeHarvested ? 1 : 0,
                    GoldGained = gold,
                    EnergyCoreGained = cores,
                    Balance = backend.Balance,
                    SlotIds = slot.CanBeHarvested ? new List<int> { slot.InstanceId } : new List<int>()
                }, gen, phase, dryRun: true);
            }

            if (!slot.CanBeHarvested)
            {
                return SlotCommandResult.Success(new HarvestResponse
                {
                    Ok = true,
                    Harvested = 0,
                    Balance = backend.Balance,
                    SlotIds = new List<int>()
                }, gen, phase);
            }

            var after = backend.Harvest(slot);
            return SlotCommandResult.Success(new HarvestResponse
            {
                Ok = true,
                Harvested = 1,
                GoldGained = after.GoldIncome,
                EnergyCoreGained = after.EnergyCoreIncome,
                Balance = backend.Balance,
                SlotIds = new List<int> { after.InstanceId }
            }, gen, phase);
        }

        var harvestable = new List<SlotSnapshot>();
        foreach (var slot in backend.List())
        {
            if (slot.CanBeHarvested)
                harvestable.Add(slot);
        }

        if (dryRun)
        {
            var gold = 0;
            var cores = 0;
            var ids = new List<int>();
            foreach (var slot in harvestable)
            {
                gold += slot.GoldIncome;
                cores += slot.EnergyCoreIncome;
                ids.Add(slot.InstanceId);
            }

            return SlotCommandResult.Success(new HarvestResponse
            {
                Ok = true,
                DryRun = true,
                Harvested = harvestable.Count,
                GoldGained = gold,
                EnergyCoreGained = cores,
                Balance = backend.Balance,
                SlotIds = ids
            }, gen, phase, dryRun: true);
        }

        var applied = new List<int>();
        var gained = 0;
        var coreGained = 0;
        foreach (var slot in harvestable)
        {
            var after = backend.Harvest(slot);
            applied.Add(after.InstanceId);
            gained += after.GoldIncome;
            coreGained += after.EnergyCoreIncome;
        }

        return SlotCommandResult.Success(new HarvestResponse
        {
            Ok = true,
            Harvested = applied.Count,
            GoldGained = gained,
            EnergyCoreGained = coreGained,
            Balance = backend.Balance,
            SlotIds = applied
        }, gen, phase);
    }

    public static SlotCommandResult BuildOrUpgrade(
        ISlotBackend backend,
        int instanceId,
        int? generation,
        bool dryRun,
        bool teleportKingNearby,
        string action)
    {
        if (!TryBeginMutate(backend, $"/slots/{{id}}/{action}", out var fail))
            return fail!;

        var phase = backend.Phase;
        var gen = backend.Generation;
        var slot = backend.Resolve(instanceId, generation, out var error);
        if (slot == null)
            return IdError(error, phase, gen, instanceId);

        if (slot.IsWaitingForChoice || slot.ChoiceCoroutineRunning ||
            (backend.ChoiceBusy && !slot.IsWaitingForChoice))
        {
            var waiting = Describe(slot, action, backend.Balance, applied: false, needsChoice: true);
            var blocked = SlotCommandResult.Fail(
                409,
                ErrorCodes.ChoiceRequired,
                backend.ChoiceBusy && !slot.IsWaitingForChoice && !slot.ChoiceCoroutineRunning
                    ? "another upgrade choice is already in progress"
                    : $"slot {slot.BuildingName} is waiting for an upgrade choice",
                phase,
                gen);
            blocked.Payload = waiting;
            blocked.NeedsChoiceWait = false;
            return blocked;
        }

        var cost = slot.NextUpgradeOrBuildCost;
        var coreCost = slot.NextUpgradeOrBuildEnergyCoreCost;
        var needsChoice = slot.NextUpgradeIsChoice;
        var canPay = backend.Balance >= cost && backend.EnergyCoreBalance >= coreCost;
        var blockedReason = !slot.CanBeUpgraded
            ? "cannot_upgrade"
            : !canPay ? ErrorCodes.InsufficientGold : null;

        if (dryRun)
        {
            var would = new DryRunResponse
            {
                Ok = true,
                DryRun = true,
                Would = new DryRunWouldDto
                {
                    Action = action,
                    Slot = slot.BuildingName,
                    Cost = cost,
                    BalanceAfter = canPay ? backend.Balance - cost : backend.Balance,
                    Blocked = blockedReason != null
                }
            };
            var payload = Describe(slot, action, would.Would.BalanceAfter, applied: false, needsChoice);
            payload.DryRun = true;
            payload.Cost = cost;
            payload.EnergyCoreCost = coreCost;
            return new SlotCommandResult
            {
                Ok = true,
                Payload = new { ok = true, dryRun = true, would = would.Would, slot = payload },
                Phase = phase,
                Generation = gen,
                DryRun = true
            };
        }

        if (blockedReason == ErrorCodes.InsufficientGold)
        {
            return SlotCommandResult.Fail(
                409,
                ErrorCodes.InsufficientGold,
                $"need {cost} gold and {coreCost} energy cores, have {backend.Balance}/{backend.EnergyCoreBalance}",
                phase,
                gen);
        }

        if (blockedReason != null)
        {
            return SlotCommandResult.Fail(
                409,
                blockedReason,
                $"slot {slot.BuildingName} cannot be built or upgraded",
                phase,
                gen);
        }

        var teleport = teleportKingNearby || PluginConfig.TeleportKingNearbyOnSlotAction;
        var after = backend.BuildOrUpgrade(slot, teleport);
        var waitingChoice = after.IsWaitingForChoice || after.ChoiceCoroutineRunning || after.NextUpgradeIsChoice;
        var result = SlotCommandResult.Success(
            Describe(after, action, backend.Balance, applied: !waitingChoice || after.Level != slot.Level, needsChoice: waitingChoice && after.Choices.Count > 1),
            gen,
            phase);
        result.NeedsChoiceWait = after.NextUpgradeIsChoice && !after.IsWaitingForChoice && after.Choices.Count > 1;
        return result;
    }

    public static SlotCommandResult PollChoice(ISlotBackend backend, int instanceId, int? generation, string action)
    {
        backend.PumpChoiceWait();
        var slot = backend.Refresh(instanceId);
        slot ??= backend.Resolve(instanceId, generation, out _);
        if (slot == null)
            return SlotCommandResult.Fail(404, ErrorCodes.NotFound, $"slot {instanceId} not found", backend.Phase, backend.Generation);

        var needsChoice = slot.IsWaitingForChoice || slot.ChoiceCoroutineRunning || (slot.NextUpgradeIsChoice && slot.Choices.Count > 1);
        return SlotCommandResult.Success(
            Describe(slot, action, backend.Balance, applied: !needsChoice, needsChoice),
            backend.Generation,
            backend.Phase);
    }

    public static SlotCommandResult Choose(
        ISlotBackend backend,
        int instanceId,
        int? generation,
        string? choiceName,
        bool dryRun)
    {
        if (!TryBeginMutate(backend, "/slots/{id}/choice", out var fail))
            return fail!;

        var phase = backend.Phase;
        var gen = backend.Generation;
        var slot = backend.Resolve(instanceId, generation, out var error);
        if (slot == null)
            return IdError(error, phase, gen, instanceId);

        var pending = slot.IsWaitingForChoice || slot.ChoiceCoroutineRunning || slot.Choices.Count > 0;
        if (!pending && !slot.NextUpgradeIsChoice)
        {
            return SlotCommandResult.Fail(
                409,
                ErrorCodes.ChoiceRequired,
                $"slot {slot.BuildingName} is not waiting for a choice",
                phase,
                gen);
        }

        if (string.IsNullOrWhiteSpace(choiceName))
        {
            return SlotCommandResult.Fail(
                409,
                ErrorCodes.ChoiceRequired,
                "choice name is required",
                phase,
                gen);
        }

        if (dryRun)
        {
            return SlotCommandResult.Success(new DryRunResponse
            {
                Ok = true,
                DryRun = true,
                Would = new DryRunWouldDto
                {
                    Action = "choice",
                    Slot = slot.BuildingName,
                    Cost = 0,
                    BalanceAfter = backend.Balance,
                    Blocked = false
                }
            }, gen, phase, dryRun: true);
        }

        var after = backend.CompleteChoice(slot, choiceName, out var choiceError);
        if (choiceError != null)
        {
            var code = choiceError == ErrorCodes.NotFound ? ErrorCodes.NotFound : ErrorCodes.ChoiceRequired;
            var status = code == ErrorCodes.NotFound ? 404 : 409;
            return SlotCommandResult.Fail(status, code, choiceError == ErrorCodes.NotFound
                ? $"choice '{choiceName}' not found"
                : choiceError, phase, gen);
        }

        var result = SlotCommandResult.Success(
            Describe(after, "choice", backend.Balance, applied: true, needsChoice: after.IsWaitingForChoice),
            gen,
            phase);
        result.NeedsChoiceWait = after.IsWaitingForChoice || after.ChoiceCoroutineRunning;
        return result;
    }

    public static SlotCommandResult CancelChoice(ISlotBackend backend, bool dryRun)
    {
        if (!TryBeginMutate(backend, "/slots/choice/cancel", out var fail))
            return fail!;

        var phase = backend.Phase;
        var gen = backend.Generation;
        if (!HasCancelableChoice(backend))
        {
            return SlotCommandResult.Fail(
                409,
                ErrorCodes.NotFound,
                "there is no upgrade choice to cancel",
                phase,
                gen);
        }

        if (dryRun)
        {
            return SlotCommandResult.Success(new DryRunResponse
            {
                Ok = true,
                DryRun = true,
                Would = new DryRunWouldDto
                {
                    Action = "cancel",
                    Cost = 0,
                    BalanceAfter = backend.Balance,
                    Blocked = false
                }
            }, gen, phase, dryRun: true);
        }

        if (!backend.CancelActiveChoice(out var error))
        {
            return SlotCommandResult.Fail(
                409,
                error ?? ErrorCodes.NotFound,
                error == ErrorCodes.UnsupportedInThisBuild
                    ? "ChoiceManager.CancelChoice is not available in this process"
                    : "there is no upgrade choice to cancel",
                phase,
                gen);
        }

        return SlotCommandResult.Success(new CancelChoiceResponse
        {
            Ok = true,
            Canceled = true,
            Phase = phase,
            Generation = gen
        }, gen, phase);
    }

    static bool HasCancelableChoice(ISlotBackend backend)
    {
        if (backend.ChoiceBusy)
            return true;
        foreach (var slot in backend.List())
        {
            if (slot.IsWaitingForChoice || slot.ChoiceCoroutineRunning)
                return true;
        }

        return false;
    }

    static bool TryBeginMutate(ISlotBackend backend, string path, out SlotCommandResult? fail)
    {
        fail = null;
        if (!backend.IsReady)
        {
            fail = SlotCommandResult.Fail(
                409,
                ErrorCodes.UnsupportedInThisBuild,
                "slot game types are not available in this process",
                backend.Phase,
                backend.Generation);
            return false;
        }

        var blocked = MutateGuard.Check(
            backend.TransitionInProgress,
            backend.Phase,
            "scene transition is running",
            $"POST {path} is illegal in phase={backend.Phase}",
            DayPhase);
        if (blocked is { } gate)
        {
            fail = SlotCommandResult.Fail(
                gate.Status,
                gate.Code,
                gate.Message,
                backend.Phase,
                backend.Generation);
            return false;
        }

        return true;
    }

    static SlotCommandResult IdError(string? error, string phase, int generation, int instanceId)
    {
        if (error == ErrorCodes.StaleId)
        {
            return SlotCommandResult.Fail(
                409,
                ErrorCodes.StaleId,
                $"stale slot id {instanceId}",
                phase,
                generation);
        }

        return SlotCommandResult.Fail(
            404,
            ErrorCodes.NotFound,
            $"slot {instanceId} not found",
            phase,
            generation);
    }

    static SlotMutateResponse Describe(SlotSnapshot slot, string action, int balance, bool applied, bool needsChoice) =>
        new()
        {
            Ok = true,
            Action = action,
            Slot = slot.BuildingName,
            Id = new EntityId
            {
                InstanceId = slot.InstanceId,
                Generation = slot.Generation,
                Kind = "slot",
                Name = slot.BuildingName
            },
            Level = slot.Level,
            Cost = slot.NextUpgradeOrBuildCost,
            EnergyCoreCost = slot.NextUpgradeOrBuildEnergyCoreCost,
            Balance = balance,
            NeedsChoice = needsChoice,
            Applied = applied,
            Choices = slot.Choices ?? new List<ChoiceDto>(),
            IsWaitingForChoice = slot.IsWaitingForChoice || slot.ChoiceCoroutineRunning
        };
}

public sealed class MemorySlotBackend : ISlotBackend
{
    readonly Dictionary<int, MemorySlot> _slots = new();

    public MemorySlotBackend()
    {
        Ids.BeginScene();
    }

    public IdRegistry Ids { get; } = new();

    public string Phase { get; set; } = Slots.DayPhase;
    public bool TransitionInProgress { get; set; }
    public int Balance { get; set; }
    public int EnergyCoreBalance { get; set; }
    public bool IsReady { get; set; } = true;
    public int CancelActiveChoiceCalls { get; private set; }
    public bool ChoiceBusy
    {
        get
        {
            foreach (var slot in _slots.Values)
            {
                if (slot.IsWaitingForChoice || slot.ChoiceCoroutineRunning || slot.PendingChoice)
                    return true;
            }

            return false;
        }
    }
    public int Generation => Ids.SceneGeneration;

    public MemorySlot Add(MemorySlot slot)
    {
        _slots[slot.InstanceId] = slot;
        Ids.Register(slot.InstanceId, "slot", slot.BuildingName, slot);
        return slot;
    }

    public MemorySlot Get(int instanceId) => _slots[instanceId];

    public void AdvanceScene()
    {
        Ids.BeginScene();
        _slots.Clear();
    }

    public SlotSnapshot? Resolve(int instanceId, int? generation, out string? error)
    {
        var gen = generation ?? Ids.SceneGeneration;
        if (!Ids.TryResolve<MemorySlot>(instanceId, gen, out var slot, out error))
            return null;
        return Snapshot(slot!);
    }

    public IReadOnlyList<SlotSnapshot> List()
    {
        var list = new List<SlotSnapshot>(_slots.Count);
        foreach (var slot in _slots.Values)
            list.Add(Snapshot(slot));
        return list;
    }

    public SlotSnapshot Harvest(SlotSnapshot snapshot)
    {
        var slot = _slots[snapshot.InstanceId];
        if (!slot.CanBeHarvested || slot.HarvestedToday)
            return Snapshot(slot);

        slot.HarvestedToday = true;
        slot.CanBeHarvested = false;
        Balance += slot.GoldIncome;
        EnergyCoreBalance += slot.EnergyCoreIncome;
        return Snapshot(slot);
    }

    public SlotSnapshot BuildOrUpgrade(SlotSnapshot snapshot, bool teleportKingNearby)
    {
        _ = teleportKingNearby;
        var slot = _slots[snapshot.InstanceId];
        Balance -= slot.NextUpgradeOrBuildCost;
        EnergyCoreBalance -= slot.NextUpgradeOrBuildEnergyCoreCost;
        slot.TeleportedKing = teleportKingNearby || PluginConfig.TeleportKingNearbyOnSlotAction;

        if (slot.NextUpgradeIsChoice && slot.Choices.Count > 1)
        {
            slot.PendingChoice = true;
            slot.PaidForPendingChoice = true;
            if (slot.ChoiceReadyDelayFrames <= 0)
            {
                slot.IsWaitingForChoice = true;
                slot.ChoiceCoroutineRunning = true;
            }
            else
            {
                slot.ChoiceDelayRemaining = slot.ChoiceReadyDelayFrames;
                slot.IsWaitingForChoice = false;
                slot.ChoiceCoroutineRunning = false;
            }

            return Snapshot(slot);
        }

        slot.Level++;
        slot.CanBeUpgraded = slot.RemainUpgradableAfterBuild;
        slot.NextUpgradeIsChoice = false;
        return Snapshot(slot);
    }

    public SlotSnapshot CompleteChoice(SlotSnapshot snapshot, string choiceName, out string? error)
    {
        var slot = _slots[snapshot.InstanceId];
        error = null;
        ChoiceDto? match = null;
        foreach (var choice in slot.Choices)
        {
            if (string.Equals(choice.Name, choiceName, StringComparison.OrdinalIgnoreCase))
            {
                match = choice;
                break;
            }
        }

        if (match == null)
        {
            error = ErrorCodes.NotFound;
            return Snapshot(slot);
        }

        if (!slot.PendingChoice && !slot.IsWaitingForChoice && !slot.NextUpgradeIsChoice)
        {
            error = ErrorCodes.ChoiceRequired;
            return Snapshot(slot);
        }

        slot.SelectedChoice = match.Name;
        slot.Level++;
        slot.PendingChoice = false;
        slot.IsWaitingForChoice = false;
        slot.ChoiceCoroutineRunning = false;
        slot.NextUpgradeIsChoice = false;
        slot.CanBeUpgraded = slot.RemainUpgradableAfterBuild;
        slot.ChoiceDelayRemaining = 0;
        return Snapshot(slot);
    }

    public bool CancelActiveChoice(out string? error)
    {
        CancelActiveChoiceCalls++;
        error = null;
        var canceled = false;
        foreach (var slot in _slots.Values)
        {
            if (!slot.IsWaitingForChoice && !slot.ChoiceCoroutineRunning && !slot.PendingChoice)
                continue;

            slot.PendingChoice = false;
            slot.IsWaitingForChoice = false;
            slot.ChoiceCoroutineRunning = false;
            slot.ChoiceDelayRemaining = 0;
            canceled = true;
        }

        if (!canceled)
        {
            error = ErrorCodes.NotFound;
            return false;
        }

        return true;
    }

    public SlotSnapshot Refresh(int instanceId)
    {
        return _slots.TryGetValue(instanceId, out var slot) ? Snapshot(slot) : new SlotSnapshot { InstanceId = instanceId };
    }

    public void PumpChoiceWait()
    {
        foreach (var slot in _slots.Values)
        {
            if (!slot.PendingChoice || slot.IsWaitingForChoice)
                continue;
            if (slot.ChoiceDelayRemaining > 0)
                slot.ChoiceDelayRemaining--;
            if (slot.ChoiceDelayRemaining <= 0)
            {
                slot.IsWaitingForChoice = true;
                slot.ChoiceCoroutineRunning = true;
            }
        }
    }

    SlotSnapshot Snapshot(MemorySlot slot) =>
        new()
        {
            InstanceId = slot.InstanceId,
            Generation = Ids.SceneGeneration,
            BuildingName = slot.BuildingName,
            Level = slot.Level,
            NextUpgradeOrBuildCost = slot.NextUpgradeOrBuildCost,
            NextUpgradeOrBuildEnergyCoreCost = slot.NextUpgradeOrBuildEnergyCoreCost,
            CanBeUpgraded = slot.CanBeUpgraded,
            NextUpgradeIsChoice = slot.NextUpgradeIsChoice,
            CanBeHarvested = slot.CanBeHarvested && !slot.HarvestedToday,
            HarvestedToday = slot.HarvestedToday,
            IsWaitingForChoice = slot.IsWaitingForChoice,
            ChoiceCoroutineRunning = slot.ChoiceCoroutineRunning,
            GoldIncome = slot.GoldIncome,
            EnergyCoreIncome = slot.EnergyCoreIncome,
            Position = slot.Position,
            Choices = slot.Choices
        };
}

public sealed class MemorySlot
{
    public int InstanceId { get; set; }
    public string BuildingName { get; set; } = "";
    public int Level { get; set; }
    public int NextUpgradeOrBuildCost { get; set; }
    public int NextUpgradeOrBuildEnergyCoreCost { get; set; }
    public bool CanBeUpgraded { get; set; } = true;
    public bool NextUpgradeIsChoice { get; set; }
    public bool CanBeHarvested { get; set; }
    public bool HarvestedToday { get; set; }
    public bool IsWaitingForChoice { get; set; }
    public bool ChoiceCoroutineRunning { get; set; }
    public bool PendingChoice { get; set; }
    public bool PaidForPendingChoice { get; set; }
    public bool RemainUpgradableAfterBuild { get; set; }
    public bool TeleportedKing { get; set; }
    public int GoldIncome { get; set; }
    public int EnergyCoreIncome { get; set; }
    public int ChoiceReadyDelayFrames { get; set; }
    public int ChoiceDelayRemaining { get; set; }
    public string? SelectedChoice { get; set; }
    public Vec3Dto Position { get; set; } = new();
    public List<ChoiceDto> Choices { get; set; } = new();
}
