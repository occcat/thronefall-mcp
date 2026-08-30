using System.Collections.Generic;

namespace ThronefallControl.Dto;

public sealed class NextWaveDto
{
    public bool Available { get; set; }
    public int WaveNumber { get; set; }
    public int OutOfWaves { get; set; }
    public int GoldReward { get; set; }
    public float DifficultyMulti { get; set; }
    public string WarningText { get; set; } = "";
    public List<NextWaveGroupDto> Groups { get; set; } = new();
    public List<NextWaveEnemyDto> Enemies { get; set; } = new();
}

public sealed class NextWaveGroupDto
{
    public EntityId Spawn { get; set; } = new() { Kind = "spawn" };
    public string EnemyName { get; set; } = "";
    public int Count { get; set; }
    public bool Elite { get; set; }
    public int GoldCoins { get; set; }
    public float Delay { get; set; }
    public float Interval { get; set; }
    public Vec3Dto SuggestedRally { get; set; } = new();
}

public sealed class NextWaveEnemyDto
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public bool Elite { get; set; }
    public float MaxHp { get; set; }
    public float Speed { get; set; }
    public float Range { get; set; }
    public float AttackDamage { get; set; }
    public float AttackCooldown { get; set; }
}
