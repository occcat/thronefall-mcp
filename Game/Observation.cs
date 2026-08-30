using System;
using System.Collections.Generic;
using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public sealed class LiveWorld : IWorld
{
    public WorldHints Hints() => Observation.ReadHints();

    public void Capture(GameFacade facade, StateDto dto, StateInclude include) =>
        Observation.Capture(facade, dto, include);
}

public static class Observation
{
    public static WorldHints ReadHints()
    {
        var hints = new WorldHints { SceneName = UnityAccess.ActiveSceneName() };
        try
        {
            var stm = UnityAccess.Singleton("SceneTransitionManager");
            hints.TransitionRunning = UnityAccess.Bool(stm, "SceneTransitionIsRunning");
            hints.SceneState = UnityAccess.EnumName(stm, "CurrentSceneState");
            hints.InLevelSelect = UnityAccess.Call(stm, "IsInLevelSelect") is true;
        }
        catch
        {
            // boot / missing singleton
        }

        try
        {
            var dnc = UnityAccess.Singleton("DayNightCycle", "Instance");
            hints.Timestate = UnityAccess.EnumName(dnc, "CurrentTimestate");
        }
        catch
        {
            // menu
        }

        try
        {
            var gs = UnityAccess.Singleton("LocalGamestate", "Instance")
                     ?? UnityAccess.Singleton("LocalGamestate");
            hints.MatchState = UnityAccess.EnumName(gs, "CurrentState");
            hints.EndScreenVisible = UnityAccess.Bool(gs, "endScreenShownThisMatch");
        }
        catch
        {
            // ignore
        }

        if (!hints.EndScreenVisible)
            hints.EndScreenVisible = EndScreenUiActive();

        return hints;
    }

    public static void Capture(GameFacade facade, StateDto dto, StateInclude include)
    {
        var ids = facade.Ids;
        dto.Level = ReadLevel();
        dto.Economy = ReadEconomy();
        dto.Clock = ReadClock();
        dto.King = ReadKing(ids);
        dto.Settings = ReadSettings();
        if (include.WantsLoadout)
            dto.Loadout = ReadLoadout();
        if (include.WantsSlots)
            dto.Slots = ReadSlots(ids);
        if (include.WantsUnits)
            dto.Units = ReadUnits(ids);
        if (include.WantsTraining)
            dto.Training = Training.ReadAll();
        if (include.WantsEnemies)
            dto.Enemies = ReadEnemies(ids);
        if (include.WantsSpawns)
            dto.Spawns = ReadSpawns(ids);
        if (include.WantsNextWave)
            dto.NextWave = NextWave.Read(ids);
        if (include.WantsCutters)
            dto.Cutters = ReadCutters(ids);
    }

    public static LevelDto ReadLevel()
    {
        var dto = new LevelDto { SceneName = UnityAccess.ActiveSceneName() };
        try
        {
            var lpm = UnityAccess.Singleton("LevelProgressManager");
            var info = UnityAccess.Call(lpm, "GetLevelInfoFromCurrentSceneName")
                       ?? UnityAccess.Call(lpm, "GetLevelInfoFromSceneName", dto.SceneName);
            if (info != null)
            {
                dto.SceneName = UnityAccess.String(info, "sceneName", dto.SceneName);
                dto.DisplayName = UnityAccess.String(info, "displayName");
                if (string.IsNullOrEmpty(dto.DisplayName))
                    dto.DisplayName = UnityAccess.String(info, "LocalizedDisplayName");
                dto.Beaten = UnityAccess.Bool(info, "Beaten");
            }

            var data = UnityAccess.Call(lpm, "GetLevelDataForActiveScene")
                       ?? UnityAccess.Get(info, "LevelData");
            if (data != null)
            {
                dto.Beaten = UnityAccess.Bool(data, "beaten") || dto.Beaten;
                dto.Highscore = UnityAccess.Int(data, "highscore");
            }
        }
        catch
        {
            // keep scene name only
        }

        if (string.IsNullOrEmpty(dto.DisplayName))
            dto.DisplayName = dto.SceneName;
        return dto;
    }

    public static EconomyDto ReadEconomy()
    {
        var dto = new EconomyDto();
        try
        {
            var pi = UnityAccess.Singleton("PlayerInteraction");
            dto.Balance = UnityAccess.Int(pi, "Balance");
            dto.TrueBalance = UnityAccess.Int(pi, "TrueBalance");
            dto.EnergyCoreBalance = UnityAccess.Int(pi, "EnergyCoreBalance");
            dto.TrueEnergyCoreBalance = UnityAccess.Int(pi, "TrueEnergyCoreBalance");
            dto.Networth = UnityAccess.Int(pi, "Networth");
            dto.IsFreeToCallNight = UnityAccess.Bool(pi, "IsFreeToCallNight");
        }
        catch
        {
            // menu
        }

        try
        {
            var dnc = UnityAccess.Singleton("DayNightCycle", "Instance");
            dto.CoinCountToBeHarvested = UnityAccess.Int(dnc, "CoinCountToBeHarvested");
        }
        catch
        {
            // ignore
        }

        return dto;
    }

    public static ClockDto ReadClock()
    {
        var dto = new ClockDto();
        try
        {
            var dnc = UnityAccess.Singleton("DayNightCycle", "Instance");
            dto.Timestate = UnityAccess.EnumName(dnc, "CurrentTimestate");
            dto.RemainingAutoDayTime = UnityAccess.Float(dnc, "RemainingAutoDayTime");
            dto.RemainingAutoNightTime = UnityAccess.Float(dnc, "RemainingAutoNightTime");
            dto.AutomatedDaytime = UnityAccess.Bool(dnc, "AutomatedDaytime");
            dto.AutomatedNighttime = UnityAccess.Bool(dnc, "AutomatedNighttime");
            dto.AfterSunrise = UnityAccess.Bool(dnc, "AfterSunrise");
        }
        catch
        {
            // menu
        }

        try
        {
            var spawner = UnityAccess.Singleton("EnemySpawner");
            dto.Wavenumber = UnityAccess.Int(spawner, "Wavenumber");
            dto.WaveCount = UnityAccess.Int(spawner, "WaveCount");
            dto.SpawningInProgress = UnityAccess.Bool(spawner, "SpawningInProgress");
        }
        catch
        {
            // ignore
        }

        ScoreClock.Fill(dto);
        return dto;
    }

    public static KingDto ReadKing(IdRegistry ids)
    {
        var dto = new KingDto();
        try
        {
            var pm = UnityAccess.Singleton("PlayerMovement");
            if (pm == null)
                return dto;
            var name = UnityAccess.NameOf(pm);
            dto.Id = ids.Register(UnityAccess.InstanceId(pm), "king", name, pm);
            dto.Position = UnityAccess.PositionOf(pm);
            dto.Dead = UnityAccess.Bool(pm, "Dead");
            var hp = UnityAccess.Get(pm, "Hp");
            dto.Hp = UnityAccess.Float(hp, "HpValue");
            dto.MaxHp = UnityAccess.Float(hp, "maxHp");
            dto.Alive = UnityAccess.Bool(hp, "Alive");
            dto.Invulnerable = UnityAccess.Bool(hp, "invulnerable");
            if (dto.Dead)
                dto.Alive = false;
        }
        catch
        {
            // menu
        }

        return dto;
    }

    public static SettingsDto ReadSettings()
    {
        var dto = new SettingsDto();
        try
        {
            var sm = UnityAccess.Singleton("SettingsManager", "Instance");
            dto.ResetUnitFormationEveryMorning = UnityAccess.Bool(sm, "ResetUnitFormationEveryMorning");
            dto.EnableControlGroups = UnityAccess.Bool(sm, "EnableControlGroups");
        }
        catch
        {
            // ignore
        }

        return dto;
    }

    public static LoadoutDto ReadLoadout()
    {
        var dto = new LoadoutDto();
        try
        {
            var save = UnityAccess.GetStatic("MatchSaveLoadHandler", "CurrentSave");
            var names = UnityAccess.Get(save, "currentLoadoutAsString");
            foreach (var item in UnityAccess.Enumerate(names))
            {
                var s = item?.ToString();
                if (!string.IsNullOrEmpty(s))
                    dto.AsString.Add(s!);
            }

            if (dto.AsString.Count == 0)
            {
                var pm = UnityAccess.Singleton("PerkManager");
                foreach (var item in UnityAccess.Enumerate(UnityAccess.Get(pm, "CurrentlyEquipped")))
                {
                    var n = UnityAccess.String(item, "displayName");
                    if (!string.IsNullOrEmpty(n))
                        dto.AsString.Add(n);
                }
            }
        }
        catch
        {
            // ignore
        }

        dto.PerkPointsRemaining = ReadPerkPointsRemaining();
        return dto;
    }

    public static List<SlotDto> ReadSlots(IdRegistry ids)
    {
        var result = new List<SlotDto>();
        object[] slots;
        try
        {
            slots = UnityAccess.FindObjects("BuildSlot");
        }
        catch
        {
            return result;
        }

        var instanceBySlot = new Dictionary<object, int>();
        foreach (var slot in slots)
        {
            var iid = UnityAccess.InstanceId(slot);
            instanceBySlot[slot] = iid;
        }

        foreach (var slot in slots)
        {
            try
            {
                result.Add(ReadSlot(ids, slot, instanceBySlot));
            }
            catch
            {
                // skip broken slot
            }
        }

        return result;
    }

    public static List<UnitDto> ReadUnits(IdRegistry ids)
    {
        var result = new List<UnitDto>();
        try
        {
            var tm = UnityAccess.Singleton("TagManager");
            var units = UnityAccess.Get(tm, "PlayerUnits");
            foreach (var tagged in UnityAccess.Enumerate(units))
            {
                try
                {
                    result.Add(ReadUnit(ids, tagged));
                }
                catch
                {
                    // skip
                }
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    public static EnemySummaryDto ReadEnemies(IdRegistry ids)
    {
        var dto = new EnemySummaryDto();
        try
        {
            var tm = UnityAccess.Singleton("TagManager");
            var units = UnityAccess.Get(tm, "EnemyUnits");
            foreach (var tagged in UnityAccess.Enumerate(units))
            {
                try
                {
                    dto.Units.Add(ReadEnemy(ids, tagged));
                }
                catch
                {
                    // skip projectile-like leftovers
                }
            }
        }
        catch
        {
            return dto;
        }

        dto.Count = dto.Units.Count;
        return dto;
    }

    public static List<SpawnLineDto> ReadSpawns(IdRegistry ids)
    {
        var result = new List<SpawnLineDto>();
        object[] lines;
        try
        {
            lines = UnityAccess.FindObjects("EnemySpawnLine");
        }
        catch
        {
            return result;
        }

        var castle = FindTagged("CastleCenter");
        var castlePos = castle == null ? new Vec3Dto() : UnityAccess.PositionOf(castle);
        var hasCastle = castle != null;

        foreach (var line in lines)
        {
            try
            {
                result.Add(ReadSpawn(ids, line, castlePos, hasCastle));
            }
            catch
            {
                // skip
            }
        }

        return result;
    }

    public static List<CutterDto> ReadCutters(IdRegistry ids)
    {
        var result = new List<CutterDto>();
        object[] cutters;
        try
        {
            cutters = UnityAccess.FindObjects("CutOpenPathInteractor");
        }
        catch
        {
            return result;
        }

        foreach (var cutter in cutters)
        {
            try
            {
                var name = UnityAccess.NameOf(cutter);
                var dto = new CutterDto
                {
                    Id = ids.Register(UnityAccess.InstanceId(cutter), "cutter", name, cutter),
                    PathOpened = UnityAccess.Bool(cutter, "pathOpened"),
                    ToggleCost = UnityAccess.Int(cutter, "toggleCost"),
                    CanBeInteractedWith = UnityAccess.Bool(cutter, "CanBeInteractedWith")
                };
                var valid = UnityAccess.Call(cutter, "IsToggleValidToUse");
                dto.IsToggleValidToUse = valid is true;
                result.Add(dto);
            }
            catch
            {
                // skip
            }
        }

        return result;
    }

    static SlotDto ReadSlot(IdRegistry ids, object slot, Dictionary<object, int> instanceBySlot)
    {
        var buildingName = UnityAccess.String(slot, "buildingName");
        var name = string.IsNullOrEmpty(buildingName) ? UnityAccess.NameOf(slot) : buildingName;
        var interactor = UnityAccess.Get(slot, "Interactor") ?? UnityAccess.Get(slot, "buildingInteractor");
        var hp = UnityAccess.Get(slot, "HpParent") ?? UnityAccess.Get(interactor, "buildingHP");
        var dto = new SlotDto
        {
            Id = ids.Register(UnityAccess.InstanceId(slot), "slot", name, slot),
            BuildingName = buildingName,
            Level = UnityAccess.Int(slot, "Level"),
            State = UnityAccess.EnumName(slot, "State"),
            GoldIncome = UnityAccess.Int(slot, "GoldIncome"),
            EnergyCoreIncome = UnityAccess.Int(slot, "EnergyCoreIncome"),
            NextUpgradeOrBuildCost = UnityAccess.Int(slot, "NextUpgradeOrBuildCost"),
            NextUpgradeOrBuildEnergyCoreCost = UnityAccess.Int(slot, "NextUpgradeOrBuildEnergyCoreCost"),
            CanBeUpgraded = UnityAccess.Bool(slot, "CanBeUpgraded"),
            NextUpgradeIsChoice = UnityAccess.Bool(slot, "NextUpgradeIsChoice"),
            CanBeHarvested = UnityAccess.Bool(interactor, "canBeHarvested"),
            HarvestedToday = UnityAccess.Bool(interactor, "harvestedToday"),
            KnockedOutTonight = UnityAccess.Bool(interactor, "KnockedOutTonight"),
            IsWaitingForChoice = UnityAccess.Bool(interactor, "IsWaitingForChoice"),
            IsBlueprint = UnityAccess.Bool(slot, "IsBlueprint") || UnityAccess.EnumName(slot, "State") == "Blueprint",
            Position = UnityAccess.PositionOf(slot),
            Hp = new HpDto
            {
                Value = UnityAccess.Float(hp, "HpValue"),
                Max = UnityAccess.Float(hp, "maxHp"),
                Alive = hp != null && UnityAccess.Bool(hp, "Alive")
            },
            Combat = ReadCombat(hp, UnityAccess.Get(slot, "BuildingParent") ?? slot)
        };

        var unlocks = new SlotUnlocksDto
        {
            ActivatorLevel = UnityAccess.Int(slot, "ActivatorLevel")
        };
        foreach (var other in UnityAccess.Enumerate(UnityAccess.Get(slot, "IsRootOf")))
        {
            if (instanceBySlot.TryGetValue(other, out var iid))
                unlocks.IsRootOf.Add(iid);
            else
                unlocks.IsRootOf.Add(UnityAccess.InstanceId(other));
        }

        foreach (var other in UnityAccess.Enumerate(UnityAccess.Get(slot, "IsActivatorOf")))
        {
            if (instanceBySlot.TryGetValue(other, out var iid))
                unlocks.IsActivatorOf.Add(iid);
            else
                unlocks.IsActivatorOf.Add(UnityAccess.InstanceId(other));
        }

        var requiredRoot = UnityAccess.Get(slot, "requiredRoot");
        if (requiredRoot != null)
        {
            if (instanceBySlot.TryGetValue(requiredRoot, out var iid))
                unlocks.RequiredRoot = iid;
            else
                unlocks.RequiredRoot = UnityAccess.InstanceId(requiredRoot);
        }

        var activator = UnityAccess.Get(slot, "ActivatorBuilding");
        if (activator != null)
        {
            if (instanceBySlot.TryGetValue(activator, out var iid))
                unlocks.ActivatorBuilding = iid;
            else
                unlocks.ActivatorBuilding = UnityAccess.InstanceId(activator);
        }

        dto.Unlocks = unlocks;
        if (dto.IsWaitingForChoice)
            dto.Choices = ReadChoices();
        SlotPreview.Fill(dto, slot, instanceBySlot);
        return dto;
    }

    static List<ChoiceDto> ReadChoices()
    {
        var list = new List<ChoiceDto>();
        try
        {
            var cm = UnityAccess.Singleton("ChoiceManager");
            foreach (var choice in UnityAccess.Enumerate(UnityAccess.Get(cm, "availableChoices")))
            {
                list.Add(new ChoiceDto
                {
                    Name = UnityAccess.String(choice, "name"),
                    Tooltip = UnityAccess.String(choice, "tooltip"),
                    CanBePicked = UnityAccess.Bool(choice, "CanBePicked")
                });
            }
        }
        catch
        {
            return list;
        }

        return list;
    }

    static UnitDto ReadUnit(IdRegistry ids, object tagged)
    {
        var movement = UnityAccess.GetComponent(tagged, "PathfindMovementPlayerunit") ?? tagged;
        var hp = UnityAccess.Get(tagged, "Hp") ?? UnityAccess.Get(movement, "hp");
        var name = UnityAccess.NameOf(tagged);
        var tags = new List<string>();
        var tagIds = new List<int>();
        ETags.AddAll(UnityAccess.Get(tagged, "Tags"), tags, tagIds);
        var dto = new UnitDto
        {
            Id = ids.Register(UnityAccess.InstanceId(tagged), "unit", name, tagged),
            TypeName = name,
            Hp = UnityAccess.Float(hp, "HpValue"),
            MaxHp = UnityAccess.Float(hp, "maxHp"),
            Alive = UnityAccess.Bool(hp, "Alive"),
            Tags = tags,
            TagIds = tagIds,
            HomePosition = UnityAccess.ToVec(UnityAccess.Get(movement, "HomePosition")),
            HoldPosition = UnityAccess.Bool(movement, "HoldPosition"),
            FollowingPlayer = UnityAccess.Bool(movement, "FollowingPlayer"),
            Flying = UnityAccess.Bool(movement, "Flying"),
            Position = UnityAccess.PositionOf(tagged),
            ControlGroup = ETags.ControlGroup(tags),
            Combat = ReadCombat(hp, movement)
        };
        return dto;
    }

    static EnemyDto ReadEnemy(IdRegistry ids, object tagged)
    {
        var hp = UnityAccess.Get(tagged, "Hp");
        var name = UnityAccess.NameOf(tagged);
        var tags = new List<string>();
        var tagIds = new List<int>();
        ETags.AddAll(UnityAccess.Get(tagged, "Tags"), tags, tagIds);
        return new EnemyDto
        {
            Id = ids.Register(UnityAccess.InstanceId(tagged), "enemy", name, tagged),
            Name = name,
            Hp = UnityAccess.Float(hp, "HpValue"),
            MaxHp = UnityAccess.Float(hp, "maxHp"),
            Pos = UnityAccess.PositionOf(tagged),
            Tags = tags
        };
    }

    static SpawnLineDto ReadSpawn(IdRegistry ids, object line, Vec3Dto castlePos, bool hasCastle)
    {
        var name = UnityAccess.NameOf(line);
        var polyline = ReadPolyline(line);
        var dto = new SpawnLineDto
        {
            Id = ids.Register(UnityAccess.InstanceId(line), "spawn", name, line),
            Difficulty = UnityAccess.EnumName(line, "difficulty"),
            DifficultyBudgetMultiplyer = UnityAccess.Float(line, "DifficultyBudgetMultiplyer", 1f),
            CanSpawnFlying = UnityAccess.Bool(line, "canSpawnFlying"),
            CanSpawnSmallGround = UnityAccess.Bool(line, "canSpawnSmallGround"),
            CanSpawnBigGround = UnityAccess.Bool(line, "canSpawnBigGround"),
            Polyline = polyline,
            SuggestedRally = SuggestRally(polyline, castlePos, hasCastle)
        };
        return dto;
    }

    static List<Vec3Dto> ReadPolyline(object line)
    {
        var points = new List<Vec3Dto>();
        var spawnLine = UnityAccess.Get(line, "SpawnLine") ?? UnityAccess.Get(line, "spawnLine");
        var lr = UnityAccess.GetComponent(line, "LineRenderer")
                 ?? UnityAccess.GetComponent(spawnLine, "LineRenderer")
                 ?? UnityAccess.GetComponentInChildren(spawnLine ?? line, "LineRenderer");
        if (lr != null)
        {
            var count = UnityAccess.Int(lr, "positionCount");
            var world = UnityAccess.Bool(lr, "useWorldSpace");
            var tr = UnityAccess.TransformOf(lr);
            for (var i = 0; i < count; i++)
            {
                var p = UnityAccess.Call(lr, "GetPosition", i);
                if (!world && tr != null && p != null)
                    p = UnityAccess.Call(tr, "TransformPoint", p);
                points.Add(UnityAccess.ToVec(p));
            }
        }

        if (points.Count == 0 && spawnLine != null)
        {
            var n = UnityAccess.Int(spawnLine, "childCount");
            for (var i = 0; i < n; i++)
            {
                var child = UnityAccess.Call(spawnLine, "GetChild", i);
                points.Add(UnityAccess.PositionOf(child));
            }
        }

        if (points.Count == 0)
            points.Add(UnityAccess.PositionOf(line));
        return points;
    }

    static Vec3Dto SuggestRally(List<Vec3Dto> polyline, Vec3Dto castle, bool hasCastle)
    {
        if (polyline.Count == 0)
            return new Vec3Dto();
        if (!hasCastle)
            return polyline[0];

        var best = polyline[0];
        var bestD = DistanceSq(best, castle);
        for (var i = 1; i < polyline.Count; i++)
        {
            var d = DistanceSq(polyline[i], castle);
            if (d < bestD)
            {
                bestD = d;
                best = polyline[i];
            }
        }

        var dx = best.X - castle.X;
        var dy = best.Y - castle.Y;
        var dz = best.Z - castle.Z;
        var len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var offset = PluginConfig.WallBackOffset;
        if (len < 0.001)
            return best;
        var s = offset / (float)len;
        return new Vec3Dto
        {
            X = best.X + dx * s,
            Y = best.Y + dy * s,
            Z = best.Z + dz * s
        };
    }

    static CombatDto ReadCombat(object? hp, object? host)
    {
        var combat = new CombatDto();
        object? attack = null;
        var attacks = UnityAccess.Get(hp, "AutoAttacks") ?? UnityAccess.Get(host, "autoAttacks");
        if (attacks is Array arr && arr.Length > 0)
            attack = arr.GetValue(0);
        if (attack == null)
        {
            foreach (var item in UnityAccess.Enumerate(attacks))
            {
                attack = item;
                break;
            }
        }

        attack ??= UnityAccess.GetComponent(hp, "AutoAttack")
                   ?? UnityAccess.GetComponent(host, "AutoAttack")
                   ?? UnityAccess.GetComponentInChildren(host, "AutoAttack");
        if (attack == null)
            return combat;

        combat.AutoAttack.CooldownDuration = UnityAccess.Float(attack, "cooldownDuration");
        foreach (var prio in UnityAccess.Enumerate(UnityAccess.Get(attack, "targetPriorities")))
        {
            var tp = new TargetPriorityDto
            {
                Range = UnityAccess.Float(prio, "range"),
                MinRange = UnityAccess.Float(prio, "minRange")
            };
            var dummyIds = new List<int>();
            ETags.AddAll(UnityAccess.Get(prio, "mustHaveTags"), tp.MustHaveTags, dummyIds);
            dummyIds.Clear();
            ETags.AddAll(UnityAccess.Get(prio, "mayNotHaveTags"), tp.MayNotHaveTags, dummyIds);
            combat.AutoAttack.Priorities.Add(tp);
        }

        var weapon = UnityAccess.Get(attack, "weapon") ?? UnityAccess.Get(attack, "Weapon");
        ReadWeapon(weapon, combat.Weapon);
        return combat;
    }

    static void ReadWeapon(object? weapon, WeaponDto dto)
    {
        if (weapon == null)
            return;
        foreach (var m in UnityAccess.Enumerate(UnityAccess.Get(weapon, "directDamage")))
            dto.DirectDamage.Add(ReadModifier(m));
        foreach (var m in UnityAccess.Enumerate(UnityAccess.Get(weapon, "splashDamage")))
            dto.SplashDamage.Add(ReadModifier(m));
    }

    static DamageModifierDto ReadModifier(object mod)
    {
        var dto = new DamageModifierDto
        {
            DamageAdded = UnityAccess.Float(mod, "damageAdded"),
            DamageMultiplyer = UnityAccess.Float(mod, "damageMultiplyer", 1f)
        };
        var dummy = new List<int>();
        ETags.AddAll(UnityAccess.Get(mod, "requiredTags"), dto.RequiredTags, dummy);
        return dto;
    }

    static object? FindTagged(string tagName)
    {
        try
        {
            var tm = UnityAccess.Singleton("TagManager");
            var etagType = UnityAccess.FindType("TagManager+ETag") ?? UnityAccess.FindType("ETag");
            if (tm == null || etagType == null)
                return null;
            object tag;
            try { tag = Enum.Parse(etagType, tagName); }
            catch { return null; }

            var listType = typeof(List<>).MakeGenericType(etagType);
            var must = Activator.CreateInstance(listType);
            listType.GetMethod("Add")?.Invoke(must, new[] { tag });
            var may = Activator.CreateInstance(listType);
            var origin = Activator.CreateInstance(UnityAccess.FindType("UnityEngine.Vector3") ?? typeof(float));
            return UnityAccess.Call(tm, "FindClosestTaggedObjectWithTags", origin, must, may);
        }
        catch
        {
            return null;
        }
    }

    static int ReadPerkPointsRemaining()
    {
        var remaining = 0;
        var any = false;
        try
        {
            foreach (var group in UnityAccess.FindObjects("PerkSelectionGroup"))
            {
                any = true;
                var selectable = UnityAccess.Int(group, "selectableAmount");
                var selected = UnityAccess.Count(UnityAccess.Get(group, "selectedInMyGroup"));
                remaining += Math.Max(0, selectable - selected);
            }
        }
        catch
        {
            return 0;
        }

        if (any)
            return remaining;

        try
        {
            var lpm = UnityAccess.Singleton("LevelProgressManager");
            var info = UnityAccess.Call(lpm, "GetLevelInfoFromCurrentSceneName");
            var max = UnityAccess.Int(info, "maxPerkCount");
            var helper = UnityAccess.FindObject("LoadoutUIHelper");
            var equipped = UnityAccess.Count(UnityAccess.Get(helper, "equippedPerks"));
            return Math.Max(0, max - equipped);
        }
        catch
        {
            return 0;
        }
    }

    static bool EndScreenUiActive()
    {
        try
        {
            var ui = UnityAccess.FindObject("EndOfMatchUI");
            if (ui == null)
                return false;
            var victory = UnityAccess.Get(ui, "victoryScreen");
            var defeat = UnityAccess.Get(ui, "defeatScreen");
            return UnityAccess.Bool(victory, "activeInHierarchy") ||
                   UnityAccess.Bool(defeat, "activeInHierarchy");
        }
        catch
        {
            return false;
        }
    }

    static float DistanceSq(Vec3Dto a, Vec3Dto b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}
