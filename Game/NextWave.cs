using System;
using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

/// <summary>
/// Read-only preview of tonight's wave. Never calls PlaceMarkersForNextWave,
/// DebugSkipWave, or StartSpawning.
/// </summary>
public static class NextWave
{
    public static NextWaveDto Read(IdRegistry ids)
    {
        object? wave;
        try
        {
            var spawner = UnityAccess.Singleton("EnemySpawner");
            if (spawner == null)
                return Unavailable();

            wave = UnityAccess.Call(spawner, "GetNextWave");
            if (wave == null)
                return Unavailable();
        }
        catch
        {
            return Unavailable();
        }

        var dto = new NextWaveDto { Available = true };
        try
        {
            dto.WarningText = UnityAccess.String(wave, "warningText");
            dto.DifficultyMulti = UnityAccess.Float(wave, "difficultyMulti");

            var rallies = IndexRallies(ids);
            var spawns = UnityAccess.Get(wave, "spawns") ?? UnityAccess.Get(wave, "Spawns");
            foreach (var spawn in UnityAccess.Enumerate(spawns))
            {
                try
                {
                    dto.Groups.Add(ReadGroup(ids, spawn, rallies));
                }
                catch
                {
                    // skip a broken spawn row; keep Available
                }
            }

            var info = UnityAccess.Call(UnityAccess.Singleton("EnemySpawner"), "GetWaveInfoForNextWave")
                       ?? UnityAccess.Call(wave, "GetWaveInfo");
            if (info != null)
                FillInfo(dto, info);
        }
        catch
        {
            // GetNextWave succeeded; do not fabricate mouths if the rest fails.
        }

        dto.Mouths = GroupByMouth(dto.Groups);
        return dto;
    }

    public static List<NextWaveMouthDto> GroupByMouth(IEnumerable<NextWaveGroupDto>? groups)
    {
        var mouths = new List<NextWaveMouthDto>();
        if (groups == null)
            return mouths;

        var byKey = new Dictionary<string, NextWaveMouthDto>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            if (group == null)
                continue;

            var spawn = group.Spawn ?? new EntityId { Kind = "spawn" };
            var key = spawn.InstanceId != 0
                ? "id:" + spawn.InstanceId
                : "name:" + (spawn.Name ?? "");

            if (!byKey.TryGetValue(key, out var mouth))
            {
                mouth = new NextWaveMouthDto
                {
                    Spawn = spawn,
                    SuggestedRally = group.SuggestedRally ?? new Vec3Dto()
                };
                byKey[key] = mouth;
                mouths.Add(mouth);
            }

            NextWaveMouthEnemyDto? found = null;
            var enemyName = group.EnemyName ?? "";
            foreach (var enemy in mouth.Enemies)
            {
                if (string.Equals(enemy.Name, enemyName, StringComparison.Ordinal) &&
                    enemy.Elite == group.Elite)
                {
                    found = enemy;
                    break;
                }
            }

            if (found != null)
            {
                found.Count += group.Count;
                continue;
            }

            mouth.Enemies.Add(new NextWaveMouthEnemyDto
            {
                Name = enemyName,
                Count = group.Count,
                Elite = group.Elite,
                GoldCoins = group.GoldCoins,
                Delay = group.Delay,
                Interval = group.Interval
            });
        }

        return mouths;
    }

    static NextWaveDto Unavailable() => new()
    {
        Available = false,
        Groups = new List<NextWaveGroupDto>(),
        Enemies = new List<NextWaveEnemyDto>(),
        Mouths = new List<NextWaveMouthDto>()
    };

    static Dictionary<int, Vec3Dto> IndexRallies(IdRegistry ids)
    {
        var map = new Dictionary<int, Vec3Dto>();
        foreach (var line in Spawns.Snapshot(ids))
        {
            if (line?.Id == null)
                continue;
            map[line.Id.InstanceId] = line.SuggestedRally ?? new Vec3Dto();
        }

        return map;
    }

    static NextWaveGroupDto ReadGroup(IdRegistry ids, object spawn, Dictionary<int, Vec3Dto> rallies)
    {
        var line = UnityAccess.Get(spawn, "spawnLine") ?? UnityAccess.Get(spawn, "SpawnLine");
        EntityId spawnId;
        var rally = new Vec3Dto();
        if (line != null)
        {
            var name = UnityAccess.NameOf(line);
            var iid = UnityAccess.InstanceId(line);
            spawnId = ids.Register(iid, "spawn", name, line);
            if (rallies.TryGetValue(iid, out var suggested))
                rally = suggested;
        }
        else
        {
            var iid = UnityAccess.InstanceId(spawn);
            spawnId = ids.Register(iid, "spawn", "", spawn);
        }

        var prefab = UnityAccess.Get(spawn, "enemyPrefab") ?? UnityAccess.Get(spawn, "EnemyPrefab");
        return new NextWaveGroupDto
        {
            Spawn = spawnId,
            EnemyName = UnityAccess.NameOf(prefab),
            Count = UnityAccess.Int(spawn, "count"),
            Elite = UnityAccess.Bool(spawn, "eliteEnemies"),
            GoldCoins = UnityAccess.Int(spawn, "goldCoins"),
            Delay = UnityAccess.Float(spawn, "delay"),
            Interval = UnityAccess.Float(spawn, "interval"),
            SuggestedRally = rally
        };
    }

    static void FillInfo(NextWaveDto dto, object info)
    {
        dto.WaveNumber = UnityAccess.Int(info, "waveNumber");
        dto.OutOfWaves = UnityAccess.Int(info, "outOfWaves");
        dto.GoldReward = UnityAccess.Int(info, "goldReward");
        var multi = UnityAccess.Float(info, "difficultyMulti");
        if (dto.DifficultyMulti == 0f && multi != 0f)
            dto.DifficultyMulti = multi;

        var enemies = UnityAccess.Get(info, "enemies") ?? UnityAccess.Get(info, "Enemies");
        foreach (var enemy in UnityAccess.Enumerate(enemies))
        {
            dto.Enemies.Add(new NextWaveEnemyDto
            {
                Name = UnityAccess.String(enemy, "enemyName"),
                Count = UnityAccess.Int(enemy, "enemyCount"),
                Elite = UnityAccess.Bool(enemy, "eliteEnemy"),
                MaxHp = UnityAccess.Float(enemy, "maxHP"),
                Speed = UnityAccess.Float(enemy, "speed"),
                Range = UnityAccess.Float(enemy, "range"),
                AttackDamage = UnityAccess.Float(enemy, "attackDamage"),
                AttackCooldown = UnityAccess.Float(enemy, "attackCooldown")
            });
        }
    }
}
