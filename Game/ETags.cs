using System;
using System.Collections;
using System.Collections.Generic;

namespace ThronefallControl.Game;

public static class ETags
{
    // Names from TagManager.ETag in v2.13. Values follow the runtime enum when present.
    static readonly Dictionary<int, string> NamesByValue = new()
    {
        [0] = "NONE",
        [1] = "PlayerOwned",
        [2] = "EnemyOwned",
        [3] = "Player",
        [4] = "CastleCenter",
        [5] = "MeeleFighter",
        [6] = "RangedFighter",
        [7] = "Flying",
        [8] = "PlayerUnit",
        [9] = "Building",
        [10] = "SiegeWeapon",
        [11] = "AUTO_Alive",
        [12] = "AUTO_KnockedOutAndHealOnDawn",
        [13] = "Wall",
        [14] = "InfrastructureEconomy",
        [17] = "FastMoving",
        [18] = "ArmoredAgainstRanged",
        [19] = "VulnerableVsRanged",
        [20] = "Monster",
        [21] = "House",
        [22] = "WallOrTower",
        [23] = "AUTO_Commanded",
        [24] = "TakesIncreasedDamageFromTowers",
        [25] = "Tower",
        [45] = "Group1",
        [46] = "Group2",
        [47] = "Group3"
    };

    public static string NameOf(int value)
    {
        if (NamesByValue.TryGetValue(value, out var name))
            return name;
        return value.ToString();
    }

    public static void Add(object? tag, List<string> names, List<int> ids)
    {
        if (tag == null)
            return;

        int id;
        try
        {
            id = Convert.ToInt32(tag);
        }
        catch
        {
            return;
        }

        var name = tag is Enum ? tag.ToString() ?? "" : "";
        if (string.IsNullOrEmpty(name) || IsNumeric(name))
            name = NameOf(id);
        names.Add(name);
        ids.Add(id);
    }

    public static void AddAll(object? tags, List<string> names, List<int> ids)
    {
        if (tags is not IEnumerable enumerable)
            return;
        foreach (var tag in enumerable)
            Add(tag, names, ids);
    }

    public static int ControlGroup(List<string> names)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (names[i] == "Group1") return 1;
            if (names[i] == "Group2") return 2;
            if (names[i] == "Group3") return 3;
        }

        return 0;
    }

    static bool IsNumeric(string value)
    {
        if (value.Length == 0)
            return true;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] < '0' || value[i] > '9')
                return false;
        }

        return true;
    }
}
