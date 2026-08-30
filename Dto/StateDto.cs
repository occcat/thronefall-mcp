using System.Collections.Generic;
using Newtonsoft.Json;

namespace ThronefallControl.Dto;

public sealed class StateDto
{
    public bool Ok { get; set; } = true;
    public int Generation { get; set; }
    public string Phase { get; set; } = "";
    public string Scene { get; set; } = "";
    public LevelDto Level { get; set; } = new();
    public EconomyDto Economy { get; set; } = new();
    public ClockDto Clock { get; set; } = new();
    public KingDto King { get; set; } = new();
    public SettingsDto Settings { get; set; } = new();

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public LoadoutDto? Loadout { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<SlotDto>? Slots { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<UnitDto>? Units { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public EnemySummaryDto? Enemies { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<SpawnLineDto>? Spawns { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<CutterDto>? Cutters { get; set; }

    public string NightPolicy { get; set; } = NightPolicies.Human;
}

public sealed class LevelDto
{
    public string SceneName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Beaten { get; set; }
    public int Highscore { get; set; }
}

public sealed class EconomyDto
{
    public int Balance { get; set; }
    public int TrueBalance { get; set; }
    public int EnergyCoreBalance { get; set; }
    public int TrueEnergyCoreBalance { get; set; }
    public int Networth { get; set; }
    public int CoinCountToBeHarvested { get; set; }
    public bool IsFreeToCallNight { get; set; }
}

public sealed class ClockDto
{
    public string Timestate { get; set; } = "";
    public float RemainingAutoDayTime { get; set; }
    public float RemainingAutoNightTime { get; set; }
    public bool AutomatedDaytime { get; set; }
    public bool AutomatedNighttime { get; set; }
    public bool AfterSunrise { get; set; }
    public int Wavenumber { get; set; }
    public int WaveCount { get; set; }
    public bool SpawningInProgress { get; set; }
}

public sealed class KingDto
{
    public EntityId Id { get; set; } = new() { Kind = "king" };
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public bool Alive { get; set; }
    public bool Dead { get; set; }
    public Vec3Dto Position { get; set; } = new();
    public bool Invulnerable { get; set; }
}

public sealed class SettingsDto
{
    public bool ResetUnitFormationEveryMorning { get; set; }
    public bool EnableControlGroups { get; set; }
}

public sealed class LoadoutDto
{
    public List<string> AsString { get; set; } = new();
    public int PerkPointsRemaining { get; set; }
    public List<LoadoutItemDto> Catalog { get; set; } = new();
    public List<QuestDto> Quests { get; set; } = new();

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Worth { get; set; }
}

public sealed class LoadoutItemDto
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool Locked { get; set; }
    public bool Unlocked { get; set; }
    public string Description { get; set; } = "";
}

public sealed class QuestDto
{
    public string Statement { get; set; } = "";
    public bool Complete { get; set; }
}

public sealed class SlotDto
{
    public EntityId Id { get; set; } = new() { Kind = "slot" };
    public string BuildingName { get; set; } = "";
    public int Level { get; set; }
    public string State { get; set; } = "";
    public int GoldIncome { get; set; }
    public int EnergyCoreIncome { get; set; }
    public int NextUpgradeOrBuildCost { get; set; }
    public int NextUpgradeOrBuildEnergyCoreCost { get; set; }
    public bool CanBeUpgraded { get; set; }
    public bool NextUpgradeIsChoice { get; set; }
    public bool CanBeHarvested { get; set; }
    public bool HarvestedToday { get; set; }
    public bool KnockedOutTonight { get; set; }
    public bool IsWaitingForChoice { get; set; }
    public bool IsBlueprint { get; set; }
    public Vec3Dto Position { get; set; } = new();
    public HpDto Hp { get; set; } = new();
    public SlotUnlocksDto Unlocks { get; set; } = new();
    public List<ChoiceDto> Choices { get; set; } = new();
    public CombatDto Combat { get; set; } = new();
}

public sealed class HpDto
{
    public float Value { get; set; }
    public float Max { get; set; }
    public bool Alive { get; set; }
}

public sealed class SlotUnlocksDto
{
    public List<int> IsRootOf { get; set; } = new();
    public List<int> IsActivatorOf { get; set; } = new();
    public int? RequiredRoot { get; set; }
    public int? ActivatorBuilding { get; set; }
    public int ActivatorLevel { get; set; }
}

public sealed class ChoiceDto
{
    public string Name { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public bool CanBePicked { get; set; }
}

public sealed class CombatDto
{
    public AutoAttackDto AutoAttack { get; set; } = new();
    public WeaponDto Weapon { get; set; } = new();
}

public sealed class AutoAttackDto
{
    public float CooldownDuration { get; set; }
    public List<TargetPriorityDto> Priorities { get; set; } = new();
}

public sealed class TargetPriorityDto
{
    public List<string> MustHaveTags { get; set; } = new();
    public List<string> MayNotHaveTags { get; set; } = new();
    public float Range { get; set; }
    public float MinRange { get; set; }
}

public sealed class WeaponDto
{
    public List<DamageModifierDto> DirectDamage { get; set; } = new();
    public List<DamageModifierDto> SplashDamage { get; set; } = new();
}

public sealed class DamageModifierDto
{
    public List<string> RequiredTags { get; set; } = new();
    public float DamageAdded { get; set; }
    public float DamageMultiplyer { get; set; } = 1f;
}

public sealed class UnitDto
{
    public EntityId Id { get; set; } = new() { Kind = "unit" };
    public string TypeName { get; set; } = "";
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public bool Alive { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<int> TagIds { get; set; } = new();
    public Vec3Dto HomePosition { get; set; } = new();
    public bool HoldPosition { get; set; }
    public bool FollowingPlayer { get; set; }
    public bool Flying { get; set; }
    public Vec3Dto Position { get; set; } = new();
    public int ControlGroup { get; set; }
    public CombatDto Combat { get; set; } = new();
}

public sealed class EnemySummaryDto
{
    public int Count { get; set; }
    public List<EnemyDto> Units { get; set; } = new();
}

public sealed class EnemyDto
{
    public EntityId Id { get; set; } = new() { Kind = "enemy" };
    public string Name { get; set; } = "";
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public Vec3Dto Pos { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public sealed class SpawnLineDto
{
    public EntityId Id { get; set; } = new() { Kind = "spawn" };
    public string Difficulty { get; set; } = "";
    public float DifficultyBudgetMultiplyer { get; set; } = 1f;
    public bool CanSpawnFlying { get; set; }
    public bool CanSpawnSmallGround { get; set; }
    public bool CanSpawnBigGround { get; set; }
    public List<Vec3Dto> Polyline { get; set; } = new();
    public Vec3Dto SuggestedRally { get; set; } = new();
}

public sealed class CutterDto
{
    public EntityId Id { get; set; } = new() { Kind = "cutter" };
    public bool PathOpened { get; set; }
    public int ToggleCost { get; set; }
    public bool CanBeInteractedWith { get; set; }
    public bool IsToggleValidToUse { get; set; }
}

public sealed class DryRunResponse
{
    public bool Ok { get; set; } = true;
    public bool DryRun { get; set; } = true;
    public DryRunWouldDto Would { get; set; } = new();
}

public sealed class DryRunWouldDto
{
    public string Action { get; set; } = "";
    public string? Slot { get; set; }
    public string? Cutter { get; set; }
    public int Cost { get; set; }
    public int BalanceAfter { get; set; }
    public bool Blocked { get; set; }
}

public sealed class CallNightRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
}

public sealed class CallNightResponse
{
    public bool Ok { get; set; } = true;
    public bool Called { get; set; }
    public string Phase { get; set; } = "";
    public int Generation { get; set; }
}

public sealed class TogglePathRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public EntityId? Id { get; set; }
}

public sealed class TogglePathResponse
{
    public bool Ok { get; set; } = true;
    public bool PathOpened { get; set; }
    public int ToggleCost { get; set; }
    public int Generation { get; set; }
    public EntityId? Id { get; set; }
}
