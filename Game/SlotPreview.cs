using System;
using System.Collections.Generic;
using System.Reflection;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class SlotPreview
{
    const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;

    public static void Apply(
        SlotDto dto,
        string? tooltip,
        string? nextUpgradeLabel,
        IEnumerable<string>? buildingNames,
        IEnumerable<int>? slotIds)
    {
        if (dto == null)
            return;
        dto.Tooltip = tooltip ?? "";
        dto.NextUpgradeLabel = nextUpgradeLabel ?? "";
        dto.UnlockPreview = MapUnlock(buildingNames, slotIds);
    }

    public static SlotUnlockPreviewDto MapUnlock(
        IEnumerable<string>? buildingNames,
        IEnumerable<int>? slotIds)
    {
        var preview = new SlotUnlockPreviewDto();
        if (buildingNames != null)
        {
            foreach (var name in buildingNames)
            {
                if (!string.IsNullOrEmpty(name))
                    preview.BuildingNames.Add(name);
            }
        }

        if (slotIds != null)
        {
            foreach (var id in slotIds)
                preview.SlotIds.Add(id);
        }

        return preview;
    }

    public static void Fill(SlotDto dto, object? slot, IReadOnlyDictionary<object, int>? instanceBySlot)
    {
        if (dto == null)
            return;

        var tooltip = "";
        var label = "";
        var names = new List<string>();
        var ids = new List<int>();
        if (slot != null)
        {
            tooltip = ReadTooltip(slot);
            label = ReadNextUpgradeLabel(slot);
            ReadUnlockSlots(slot, instanceBySlot, names, ids);
            TryReadBlueprintPreviews(slot);
        }

        Apply(dto, tooltip, label, names, ids);
    }

    static string ReadTooltip(object slot)
    {
        try
        {
            return AsString(UnityAccess.Call(slot, "ReturnTooltip"));
        }
        catch
        {
            return "";
        }
    }

    static string ReadNextUpgradeLabel(object slot)
    {
        try
        {
            if (UnityAccess.Bool(slot, "NextUpgradeIsChoice"))
            {
                var parts = new List<string>();
                foreach (var choice in NextChoices(slot))
                {
                    var name = Translate(AsString(UnityAccess.Call(slot, "GET_LOCIDENTIFIER_CHOICENAME", choice)));
                    if (string.IsNullOrEmpty(name))
                        name = UnityAccess.String(choice, "name");
                    var desc = Translate(AsString(UnityAccess.Call(slot, "GET_LOCIDENTIFIER_CHOICEDESCRIPTION", choice)));
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(desc) &&
                        !string.Equals(desc, name, StringComparison.Ordinal))
                        parts.Add(name + ": " + desc);
                    else if (!string.IsNullOrEmpty(name))
                        parts.Add(name);
                }

                if (parts.Count > 0)
                    return string.Join(" / ", parts);
            }

            var level = UnityAccess.Int(slot, "Level");
            var upgrade = Translate(AsString(UnityAccess.Call(slot, "GET_LOCIDENTIFIER_UPGRADE", level)));
            if (!string.IsNullOrEmpty(upgrade))
                return upgrade;
            return Translate(UnityAccess.String(slot, "LOCIDENTIFIER_NAME"));
        }
        catch
        {
            return "";
        }
    }

    static IEnumerable<object> NextChoices(object slot)
    {
        var upgrades = UnityAccess.Get(slot, "Upgrades") ?? UnityAccess.Get(slot, "upgrades");
        var level = UnityAccess.Int(slot, "Level");
        object? next = null;
        var index = 0;
        foreach (var item in UnityAccess.Enumerate(upgrades))
        {
            if (index == level)
            {
                next = item;
                break;
            }

            index++;
        }

        if (next == null)
            yield break;

        foreach (var branch in UnityAccess.Enumerate(UnityAccess.Get(next, "upgradeBranches")))
        {
            var choice = UnityAccess.Get(branch, "choiceDetails");
            if (choice != null)
                yield return choice;
        }
    }

    static void ReadUnlockSlots(
        object slot,
        IReadOnlyDictionary<object, int>? instanceBySlot,
        List<string> names,
        List<int> ids)
    {
        try
        {
            var list = UnityAccess.Call(slot, "GetBuildSlotsThatWillUnlockWhenUpgraded");
            foreach (var other in UnityAccess.Enumerate(list))
            {
                var name = UnityAccess.String(other, "buildingName");
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
                if (instanceBySlot != null && instanceBySlot.TryGetValue(other, out var iid))
                    ids.Add(iid);
            }
        }
        catch
        {
            // keep whatever we already collected
        }
    }

    static void TryReadBlueprintPreviews(object slot)
    {
        try
        {
            var meshFilter = UnityAccess.FindType("UnityEngine.MeshFilter");
            if (meshFilter == null)
                return;
            var listType = typeof(List<>).MakeGenericType(meshFilter);
            var list = Activator.CreateInstance(listType);
            UnityAccess.Call(slot, "GetBlueprintPreviewsThatWillUnlockWhenUpgraded", list);
        }
        catch
        {
            // optional in this game build
        }
    }

    static string Translate(string term)
    {
        if (string.IsNullOrEmpty(term))
            return "";

        try
        {
            var t = UnityAccess.FindType("TextTranslator");
            var translated = InvokeStatic(t, "Translate", term, true, 0, true, true);
            if (!string.IsNullOrEmpty(translated))
                return translated;
        }
        catch
        {
            // try I2 next
        }

        try
        {
            var t = UnityAccess.FindType("I2.Loc.LocalizationManager");
            var translated = InvokeStatic(t, "GetTranslation", term);
            if (!string.IsNullOrEmpty(translated))
                return translated;
        }
        catch
        {
            // 拿不到则空
        }

        return "";
    }

    static string InvokeStatic(Type? type, string name, params object?[] args)
    {
        if (type == null)
            return "";
        foreach (var mi in type.GetMethods(StaticPublic))
        {
            if (mi.Name != name || !mi.IsStatic)
                continue;
            var ps = mi.GetParameters();
            if (ps.Length < args.Length)
                continue;
            try
            {
                object?[] invokeArgs;
                if (ps.Length == args.Length)
                {
                    invokeArgs = args;
                }
                else
                {
                    invokeArgs = new object?[ps.Length];
                    for (var i = 0; i < args.Length; i++)
                        invokeArgs[i] = args[i];
                    for (var i = args.Length; i < ps.Length; i++)
                    {
                        if (ps[i].HasDefaultValue)
                            invokeArgs[i] = ps[i].DefaultValue;
                        else if (ps[i].ParameterType == typeof(bool))
                            invokeArgs[i] = i == 1 || i >= 3;
                        else if (ps[i].ParameterType == typeof(int))
                            invokeArgs[i] = 0;
                    }
                }

                return AsString(mi.Invoke(null, invokeArgs));
            }
            catch
            {
                // try next overload
            }
        }

        return "";
    }

    static string AsString(object? raw)
    {
        if (raw is string s)
            return s;
        return raw?.ToString() ?? "";
    }
}
