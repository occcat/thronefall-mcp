using System;
using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class LoadoutCatalog
{
    public sealed class Source
    {
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public bool Locked { get; set; }
        public string Description { get; set; } = "";
    }

    public static void Fill(LoadoutDto dto, object? levelInfo = null)
    {
        if (dto == null)
            return;
        try { dto.Catalog = ReadCatalog(); }
        catch { dto.Catalog ??= new List<LoadoutItemDto>(); }
        try { dto.Worth = ReadWorth(); }
        catch { dto.Worth = null; }
        try { dto.Quests = levelInfo != null ? ReadQuests(levelInfo) : ReadQuestsFromScene(); }
        catch { dto.Quests ??= new List<QuestDto>(); }
    }

    public static List<LoadoutItemDto> MapCatalog(IEnumerable<Source> sources)
    {
        var list = new List<LoadoutItemDto>();
        if (sources == null)
            return list;
        foreach (var source in sources)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Name))
                continue;
            list.Add(MapItem(source.Name, source.Kind, source.Locked, source.Description));
        }

        return list;
    }

    public static LoadoutItemDto MapItem(string name, string kind, bool locked, string description = "")
    {
        return new LoadoutItemDto
        {
            Name = name?.Trim() ?? "",
            Kind = NormalizeKind(kind),
            Locked = locked,
            Unlocked = !locked,
            Description = description ?? ""
        };
    }

    public static LoadoutItemDto? MapUiItem(object ui, string fallbackKind = "")
    {
        if (ui == null)
            return null;
        var data = UnityAccess.Get(ui, "Data")
                   ?? UnityAccess.Get(ui, "equippableData")
                   ?? UnityAccess.Get(ui, "Equippable")
                   ?? UnityAccess.Get(ui, "equippable")
                   ?? ui;
        var name = UnityAccess.String(data, "displayName");
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return MapItem(name, KindOf(ui, data, fallbackKind), IsLocked(ui, data), DescriptionOf(data));
    }

    public static LoadoutItemDto? MapEquippable(object item)
    {
        if (item == null)
            return null;
        var name = UnityAccess.String(item, "displayName");
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var kind = KindFromType(item);
        if (kind.Length == 0)
            return null;
        return MapItem(name, kind, IsLocked(item, item), DescriptionOf(item));
    }

    public static QuestDto? MapQuest(object quest, object? levelData)
    {
        if (quest == null)
            return null;
        var statement = UnityAccess.Call(quest, "GetMissionStatement")?.ToString()
                        ?? UnityAccess.String(quest, "questType");
        if (string.IsNullOrWhiteSpace(statement))
            return null;
        var dto = new QuestDto { Statement = statement };
        if (levelData == null)
            return dto;
        if (UnityAccess.Call(quest, "CheckBeaten", levelData) is bool complete)
            dto.Complete = complete;
        return dto;
    }

    public static List<QuestDto> ReadQuests(object? info, object? levelData = null)
    {
        var list = new List<QuestDto>();
        if (info == null)
            return list;
        var raw = GetMember(info, "Quests", "_quests", "quests");
        var data = levelData ?? UnityAccess.Get(info, "LevelData");
        foreach (var quest in UnityAccess.Enumerate(raw))
        {
            var mapped = MapQuest(quest, data);
            if (mapped != null)
                list.Add(mapped);
        }

        return list;
    }

    static List<LoadoutItemDto> ReadCatalog()
    {
        var fromHelper = ReadFromHelper();
        if (fromHelper.Count > 0)
            return fromHelper;
        return ReadFromPerkManager();
    }

    static List<LoadoutItemDto> ReadFromHelper()
    {
        var list = new List<LoadoutItemDto>();
        var helper = UnityAccess.FindObject("LoadoutUIHelper");
        if (helper == null)
            return list;
        AppendUiList(list, UnityAccess.Get(helper, "perks"), "perk");
        AppendUiList(list, UnityAccess.Get(helper, "weapons"), "weapon");
        AppendUiList(list, UnityAccess.Get(helper, "mutators"), "mutator");
        return list;
    }

    static void AppendUiList(List<LoadoutItemDto> list, object? raw, string kind)
    {
        foreach (var ui in UnityAccess.Enumerate(raw))
        {
            var item = MapUiItem(ui, kind);
            if (item != null)
                list.Add(item);
        }
    }

    static List<LoadoutItemDto> ReadFromPerkManager()
    {
        var list = new List<LoadoutItemDto>();
        var pm = UnityAccess.Singleton("PerkManager");
        var raw = UnityAccess.Get(pm, "allEquippables");
        if (UnityAccess.Count(raw) == 0)
            raw = UnityAccess.Get(pm, "CurrentlyEquipped");
        foreach (var item in UnityAccess.Enumerate(raw))
        {
            var mapped = MapEquippable(item);
            if (mapped != null)
                list.Add(mapped);
        }

        return list;
    }

    static int? ReadWorth()
    {
        var helper = UnityAccess.FindObject("LoadoutUIHelper");
        var raw = UnityAccess.Get(helper, "LoadoutWorth");
        if (raw == null)
            return null;
        try { return Convert.ToInt32(raw); }
        catch { return null; }
    }

    static List<QuestDto> ReadQuestsFromScene()
    {
        var lpm = UnityAccess.Singleton("LevelProgressManager");
        var info = UnityAccess.Call(lpm, "GetLevelInfoFromCurrentSceneName")
                   ?? UnityAccess.Call(lpm, "GetLevelInfoFromSceneName", UnityAccess.ActiveSceneName());
        if (info == null)
            return new List<QuestDto>();
        var data = UnityAccess.Get(info, "LevelData")
                   ?? UnityAccess.Call(lpm, "GetLevelDataForActiveScene");
        return ReadQuests(info, data);
    }

    static object? GetMember(object? obj, params string[] names)
    {
        foreach (var name in names)
        {
            var value = UnityAccess.Get(obj, name);
            if (value != null)
                return value;
        }

        return null;
    }

    static bool IsLocked(object? ui, object? data)
    {
        if (UnityAccess.Get(ui, "Locked") is bool locked)
            return locked;
        if (UnityAccess.Get(data, "IsUnlocked") is bool unlocked)
            return !unlocked;
        if (UnityAccess.Get(data, "Locked") is bool dataLocked)
            return dataLocked;
        return false;
    }

    static string DescriptionOf(object? data)
    {
        var description = UnityAccess.String(data, "description");
        if (!string.IsNullOrEmpty(description))
            return description;
        var tip = UnityAccess.Call(data, "GetLockedTooltip");
        return tip?.ToString() ?? "";
    }

    static string KindOf(object? ui, object? data, string fallback)
    {
        if (ui != null)
        {
            if (IsFlag(ui, "isPerk", "IsPerk"))
                return "perk";
            if (IsFlag(ui, "isWeapon", "IsWeapon"))
                return "weapon";
            if (IsFlag(ui, "isMutator", "IsMutator"))
                return "mutator";
        }

        var fromType = KindFromType(data ?? ui);
        return fromType.Length > 0 ? fromType : NormalizeKind(fallback);
    }

    static bool IsFlag(object ui, params string[] names)
    {
        foreach (var name in names)
        {
            if (UnityAccess.Get(ui, name) is bool flag)
                return flag;
        }

        return false;
    }

    static string KindFromType(object? obj)
    {
        var name = obj?.GetType().Name ?? "";
        if (name == "PerkPoint")
            return "";
        if (name == "EquippablePerk" || name.EndsWith("Perk", StringComparison.Ordinal))
            return "perk";
        if (name == "EquippableWeapon" || name.EndsWith("Weapon", StringComparison.Ordinal))
            return "weapon";
        if (name == "EquippableMutation" || name.IndexOf("Mutat", StringComparison.Ordinal) >= 0)
            return "mutator";
        return "";
    }

    static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return "";
        var value = kind.Trim().ToLowerInvariant();
        return value is "perk" or "weapon" or "mutator" ? value : "";
    }
}
