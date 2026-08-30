using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public sealed class LiveSlotBackend : ISlotBackend
{
    readonly IdRegistry _ids = new();
    string? _sceneName;

    public bool IsReady
    {
        get
        {
            if (!ReflectionCache.SlotsReady)
                ReflectionCache.TryInit();
            return ReflectionCache.SlotsReady;
        }
    }

    public string Phase => ReadPhase(out _);
    public int Generation
    {
        get
        {
            RefreshRegistry();
            return _ids.SceneGeneration;
        }
    }

    public bool TransitionInProgress =>
        ReadPhase(out var transition) == "transition" || transition;

    public int Balance => AsInt(GetProp(Player(), ReflectionCache.Balance));
    public int EnergyCoreBalance => AsInt(GetProp(Player(), ReflectionCache.EnergyCoreBalance));
    public bool ChoiceBusy => IsTrue(GetProp(ChoiceMgr(), ReflectionCache.ChoiceCoroutineRunning));

    public SlotSnapshot? Resolve(int instanceId, int? generation, out string? error)
    {
        RefreshRegistry();
        var gen = generation ?? _ids.SceneGeneration;
        if (!_ids.TryResolve(instanceId, gen, out var target, out error))
            return null;
        return Snapshot(target!);
    }

    public IReadOnlyList<SlotSnapshot> List()
    {
        RefreshRegistry();
        var list = new List<SlotSnapshot>();
        foreach (var interactor in Interactors())
        {
            var slot = BuildingOf(interactor);
            if (slot == null)
                continue;
            list.Add(Snapshot(slot));
        }

        return list;
    }

    public SlotSnapshot Harvest(SlotSnapshot snapshot)
    {
        var player = Player();
        var slot = LiveSlot(snapshot.InstanceId);
        var interactor = InteractorOf(slot);
        if (interactor == null || player == null)
            return snapshot;

        ReflectionCache.Harvest?.Invoke(interactor, new[] { player });
        return Snapshot(slot!);
    }

    public SlotSnapshot BuildOrUpgrade(SlotSnapshot snapshot, bool teleportKingNearby)
    {
        var player = Player();
        var slot = LiveSlot(snapshot.InstanceId);
        if (slot == null || player == null)
            return snapshot;

        if (teleportKingNearby)
            TryTeleport(slot);

        var gold = AsInt(GetProp(slot, ReflectionCache.NextUpgradeOrBuildCost));
        var cores = AsInt(GetProp(slot, ReflectionCache.NextUpgradeOrBuildEnergyCoreCost));
        if (gold > 0)
            ReflectionCache.SpendCoins?.Invoke(player, new object[] { gold });
        if (cores > 0)
            ReflectionCache.SpendEnergyCores?.Invoke(player, new object[] { cores });

        ReflectionCache.TryToBuildOrUpgradeAndPay?.Invoke(slot, new[] { player, true });

        var interactor = InteractorOf(slot);
        var waiting = IsTrue(GetProp(slot, ReflectionCache.NextUpgradeIsChoice)) &&
                      (IsTrue(GetProp(ChoiceMgr(), ReflectionCache.ChoiceCoroutineRunning)) ||
                       CountChoices(slot) > 1);
        if (waiting && interactor != null)
            SetField(interactor, ReflectionCache.WaitingForChoiceField, true);

        return Snapshot(slot);
    }

    public SlotSnapshot CompleteChoice(SlotSnapshot snapshot, string choiceName, out string? error)
    {
        error = null;
        var slot = LiveSlot(snapshot.InstanceId);
        if (slot == null)
        {
            error = ErrorCodes.NotFound;
            return snapshot;
        }

        var choice = FindChoice(slot, choiceName);
        if (choice == null)
        {
            error = ErrorCodes.NotFound;
            return Snapshot(slot);
        }

        var manager = ChoiceMgr();
        var running = IsTrue(GetProp(manager, ReflectionCache.ChoiceCoroutineRunning));
        if (running)
            SetField(manager, ReflectionCache.ChoiceToReturn, choice);
        else
            ReflectionCache.OnUpgradeChoiceComplete?.Invoke(slot, new[] { choice });

        return Snapshot(slot);
    }

    public SlotSnapshot Refresh(int instanceId)
    {
        var slot = LiveSlot(instanceId);
        return slot == null ? new SlotSnapshot { InstanceId = instanceId, Generation = _ids.SceneGeneration } : Snapshot(slot);
    }

    public void PumpChoiceWait()
    {
    }

    void RefreshRegistry()
    {
        var scene = ReadSceneName();
        if (!string.Equals(scene, _sceneName, StringComparison.Ordinal))
        {
            _sceneName = scene;
            _ids.BeginScene();
        }

        foreach (var interactor in Interactors())
        {
            var slot = BuildingOf(interactor);
            if (slot == null)
                continue;
            var id = InstanceId(slot);
            if (id == 0)
                continue;
            _ids.Register(id, "slot", AsString(GetField(slot, ReflectionCache.BuildingName)), slot);
        }
    }

    SlotSnapshot Snapshot(object slot)
    {
        var interactor = InteractorOf(slot);
        var id = InstanceId(slot);
        var name = AsString(GetField(slot, ReflectionCache.BuildingName));
        var waiting = IsTrue(GetProp(interactor, ReflectionCache.IsWaitingForChoice));
        var coroutine = IsTrue(GetProp(ChoiceMgr(), ReflectionCache.ChoiceCoroutineRunning))
                        && ReferenceEquals(GetField(ChoiceMgr(), ReflectionCache.CurrentOriginBuildSlot), slot);
        return new SlotSnapshot
        {
            InstanceId = id,
            Generation = _ids.SceneGeneration,
            BuildingName = name,
            Level = AsInt(GetProp(slot, ReflectionCache.BuildSlotLevel)),
            NextUpgradeOrBuildCost = AsInt(GetProp(slot, ReflectionCache.NextUpgradeOrBuildCost)),
            NextUpgradeOrBuildEnergyCoreCost = AsInt(GetProp(slot, ReflectionCache.NextUpgradeOrBuildEnergyCoreCost)),
            CanBeUpgraded = IsTrue(GetProp(slot, ReflectionCache.CanBeUpgraded)),
            NextUpgradeIsChoice = IsTrue(GetProp(slot, ReflectionCache.NextUpgradeIsChoice)),
            CanBeHarvested = IsTrue(GetProp(interactor, ReflectionCache.CanBeHarvested)),
            HarvestedToday = IsTrue(GetField(interactor, ReflectionCache.HarvestedToday)),
            IsWaitingForChoice = waiting,
            ChoiceCoroutineRunning = coroutine,
            GoldIncome = AsInt(GetProp(slot, ReflectionCache.GoldIncome)),
            EnergyCoreIncome = AsInt(GetProp(slot, ReflectionCache.EnergyCoreIncome)),
            Position = ReadPosition(slot),
            Choices = ReadChoices(slot)
        };
    }

    object? LiveSlot(int instanceId)
    {
        RefreshRegistry();
        return _ids.TryResolve(instanceId, _ids.SceneGeneration, out var target, out _) ? target : null;
    }

    IEnumerable<object> Interactors()
    {
        var manager = Singleton(ReflectionCache.TagManagerInstance);
        var list = GetField(manager, ReflectionCache.PlayerBuildingInteractors) as IEnumerable;
        if (list == null)
            yield break;
        foreach (var item in list)
        {
            if (item != null)
                yield return item;
        }
    }

    object? BuildingOf(object interactor) =>
        GetField(interactor, ReflectionCache.TargetBuilding);

    object? InteractorOf(object? slot)
    {
        if (slot == null)
            return null;
        return GetProp(slot, ReflectionCache.BuildSlotInteractor)
               ?? GetField(slot, ReflectionCache.BuildingInteractorOnSlot);
    }

    object? Player() => Singleton(ReflectionCache.PlayerInteractionInstance);

    object? Movement() => Singleton(ReflectionCache.PlayerMovementInstance);

    object? ChoiceMgr() => Singleton(ReflectionCache.ChoiceManagerInstance);

    void TryTeleport(object slot)
    {
        var movement = Movement();
        if (movement == null || ReflectionCache.TeleportTo == null)
            return;
        var transform = GetProp(slot, ReflectionCache.Transform);
        var pos = GetProp(transform, ReflectionCache.Position);
        if (pos == null)
            return;
        ReflectionCache.TeleportTo.Invoke(movement, new[] { pos });
    }

    List<ChoiceDto> ReadChoices(object slot)
    {
        var fromManager = ChoicesFromManager(slot);
        if (fromManager.Count > 0)
            return fromManager;
        return ChoicesFromUpgrades(slot);
    }

    List<ChoiceDto> ChoicesFromManager(object slot)
    {
        var result = new List<ChoiceDto>();
        var manager = ChoiceMgr();
        if (!ReferenceEquals(GetField(manager, ReflectionCache.CurrentOriginBuildSlot), slot))
            return result;
        if (GetField(manager, ReflectionCache.AvailableChoices) is not IEnumerable list)
            return result;
        foreach (var choice in list)
        {
            if (choice != null)
                result.Add(ToChoice(choice));
        }

        return result;
    }

    List<ChoiceDto> ChoicesFromUpgrades(object slot)
    {
        var result = new List<ChoiceDto>();
        var upgrades = GetProp(slot, ReflectionCache.BuildSlotUpgrades) as IList;
        var level = AsInt(GetProp(slot, ReflectionCache.BuildSlotLevel));
        if (upgrades == null || level < 0 || level >= upgrades.Count)
            return result;
        var upgrade = upgrades[level];
        if (GetField(upgrade, ReflectionCache.UpgradeBranches) is not IEnumerable branches)
            return result;
        foreach (var branch in branches)
        {
            var choice = GetField(branch, ReflectionCache.ChoiceDetails);
            if (choice != null)
                result.Add(ToChoice(choice));
        }

        return result;
    }

    int CountChoices(object slot) => ReadChoices(slot).Count;

    object? FindChoice(object slot, string choiceName)
    {
        var manager = ChoiceMgr();
        if (GetField(manager, ReflectionCache.AvailableChoices) is IEnumerable available)
        {
            foreach (var choice in available)
            {
                if (choice != null && NamesEqual(choice, choiceName))
                    return choice;
            }
        }

        var upgrades = GetProp(slot, ReflectionCache.BuildSlotUpgrades) as IList;
        var level = AsInt(GetProp(slot, ReflectionCache.BuildSlotLevel));
        if (upgrades == null || level < 0 || level >= upgrades.Count)
            return null;
        if (GetField(upgrades[level], ReflectionCache.UpgradeBranches) is not IEnumerable branches)
            return null;
        foreach (var branch in branches)
        {
            var choice = GetField(branch, ReflectionCache.ChoiceDetails);
            if (choice != null && NamesEqual(choice, choiceName))
                return choice;
        }

        return null;
    }

    bool NamesEqual(object choice, string choiceName)
    {
        var name = AsString(GetField(choice, ReflectionCache.ChoiceName));
        return string.Equals(name, choiceName, StringComparison.OrdinalIgnoreCase);
    }

    ChoiceDto ToChoice(object choice) =>
        new()
        {
            Name = AsString(GetField(choice, ReflectionCache.ChoiceName)),
            Tooltip = AsString(GetField(choice, ReflectionCache.ChoiceTooltip)),
            CanBePicked = ReflectionCache.ChoiceCanBePicked == null || IsTrue(GetProp(choice, ReflectionCache.ChoiceCanBePicked))
        };

    Vec3Dto ReadPosition(object slot)
    {
        var transform = GetProp(slot, ReflectionCache.Transform);
        var pos = GetProp(transform, ReflectionCache.Position);
        if (pos == null)
            return new Vec3Dto();
        return new Vec3Dto
        {
            X = AsFloat(GetMember(pos, "x")),
            Y = AsFloat(GetMember(pos, "y")),
            Z = AsFloat(GetMember(pos, "z"))
        };
    }

    int InstanceId(object unityObject)
    {
        var go = GetProp(unityObject, ReflectionCache.GameObject) ?? unityObject;
        var method = ReflectionCache.GetInstanceId ?? go.GetType().GetMethod("GetInstanceID");
        if (method == null)
            return 0;
        return AsInt(method.Invoke(go, null));
    }

    string ReadPhase(out bool transition)
    {
        transition = false;
        var stm = Singleton(ReflectionCache.SceneTransitionInstance);
        transition = IsTrue(GetProp(stm, ReflectionCache.SceneTransitionIsRunning));
        if (transition)
            return "transition";

        var sceneState = GetProp(stm, ReflectionCache.CurrentSceneState)?.ToString() ?? "";
        if (sceneState.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            return "menu";
        if (sceneState.IndexOf("LevelSelect", StringComparison.OrdinalIgnoreCase) >= 0)
            return "level_select";

        var dnc = GetProp(null, ReflectionCache.DayNightInstance) ??
                  CallStatic(ReflectionCache.DayNightCycle, "get_Instance");
        var time = GetProp(dnc, ReflectionCache.CurrentTimestate)?.ToString() ?? "";
        if (time.IndexOf("Night", StringComparison.OrdinalIgnoreCase) >= 0)
            return "night";
        if (time.IndexOf("Day", StringComparison.OrdinalIgnoreCase) >= 0)
            return "day";
        if (sceneState.IndexOf("InGame", StringComparison.OrdinalIgnoreCase) >= 0)
            return "day";
        return string.IsNullOrEmpty(sceneState) ? "unknown" : "menu";
    }

    string ReadSceneName()
    {
        try
        {
            if (ReflectionCache.GetActiveScene == null)
                return _sceneName ?? "";
            var scene = ReflectionCache.GetActiveScene.Invoke(null, null);
            var name = GetMember(scene, "name");
            return name as string ?? name?.ToString() ?? "";
        }
        catch
        {
            return _sceneName ?? "";
        }
    }

    static object? Singleton(FieldInfo? field) =>
        field == null ? null : field.GetValue(null);

    static object? CallStatic(Type? type, string name)
    {
        var method = type?.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return method?.Invoke(null, null);
    }

    static object? GetProp(object? target, PropertyInfo? prop)
    {
        if (prop == null)
            return null;
        if (prop.GetMethod != null && prop.GetMethod.IsStatic)
            return prop.GetValue(null);
        return target == null ? null : prop.GetValue(target);
    }

    static object? GetField(object? target, FieldInfo? field)
    {
        if (field == null)
            return null;
        if (field.IsStatic)
            return field.GetValue(null);
        return target == null ? null : field.GetValue(target);
    }

    static void SetField(object? target, FieldInfo? field, object? value)
    {
        if (field == null || (target == null && !field.IsStatic))
            return;
        field.SetValue(target, value);
    }

    static object? GetMember(object? target, string name)
    {
        if (target == null)
            return null;
        var type = target.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
            return prop.GetValue(target);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(target);
    }

    static bool IsTrue(object? value) =>
        value is true || (value is bool b && b);

    static int AsInt(object? value) =>
        value is int i ? i : value is IConvertible c ? c.ToInt32(null) : 0;

    static float AsFloat(object? value) =>
        value is float f ? f : value is IConvertible c ? c.ToSingle(null) : 0f;

    static string AsString(object? value) => value as string ?? "";
}
