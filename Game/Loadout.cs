using System;
using System.Collections.Generic;
using System.Reflection;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public interface ILoadoutRuntime
{
    bool IsUnlocked(string name, string kind);
    bool TrySelect(string name, string kind, out string? error);
    bool TryStartLevel(string sceneName, out string? error);
}

public sealed class AllowAllLoadoutRuntime : ILoadoutRuntime
{
    public bool IsUnlocked(string name, string kind)
    {
        _ = (name, kind);
        return true;
    }

    public bool TrySelect(string name, string kind, out string? error)
    {
        _ = (name, kind);
        error = null;
        return true;
    }

    public bool TryStartLevel(string sceneName, out string? error)
    {
        _ = sceneName;
        error = null;
        return true;
    }
}

public static class Loadout
{
    public static ILoadoutRuntime? Runtime { get; set; }

    public static void Reset()
    {
        Runtime = null;
        GameReflection.Reset();
    }

    public static LoadoutSelectResult Select(string name, string kind, bool dryRun = false)
    {
        var result = new LoadoutSelectResult
        {
            Name = name?.Trim() ?? "",
            Kind = NormalizeKind(kind)
        };

        if (string.IsNullOrWhiteSpace(result.Name))
        {
            result.Ok = false;
            result.Error = ErrorCodes.NotFound;
            result.Message = "loadout name is required";
            return result;
        }

        var runtime = Runtime;
        if (runtime != null && !runtime.IsUnlocked(result.Name, result.Kind))
        {
            result.Ok = false;
            result.Error = ErrorCodes.NotFound;
            result.Message = $"equippable '{result.Name}' is locked";
            return result;
        }

        if (dryRun)
        {
            result.DryRun = true;
            result.WouldSelect = true;
            return result;
        }

        if (runtime != null && !runtime.TrySelect(result.Name, result.Kind, out var error))
        {
            result.Ok = false;
            result.Error = error ?? ErrorCodes.UnsupportedInThisBuild;
            result.Message = $"failed to select '{result.Name}'";
            return result;
        }

        result.Selected = true;
        return result;
    }

    public static LevelStartResult StartLevel(string sceneName, bool dryRun = false)
    {
        var result = new LevelStartResult { SceneName = sceneName?.Trim() ?? "" };
        if (string.IsNullOrWhiteSpace(result.SceneName))
        {
            result.Ok = false;
            result.Error = ErrorCodes.NotFound;
            result.Message = "sceneName is required";
            return result;
        }

        if (dryRun)
        {
            result.DryRun = true;
            result.WouldStart = true;
            return result;
        }

        var runtime = Runtime;
        if (runtime != null && !runtime.TryStartLevel(result.SceneName, out var error))
        {
            result.Ok = false;
            result.Error = error ?? ErrorCodes.UnsupportedInThisBuild;
            result.Message = $"failed to start '{result.SceneName}'";
            return result;
        }

        result.Started = true;
        return result;
    }

    static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return "";
        return kind.Trim().ToLowerInvariant();
    }

    public sealed class ReflectionRuntime : ILoadoutRuntime
    {
        public static ReflectionRuntime Instance { get; } = new();

        public bool IsUnlocked(string name, string kind)
        {
            foreach (var ui in EquippableUi(kind))
            {
                if (!GameReflection.NamesEqual(DisplayName(ui), name))
                    continue;
                return !IsLocked(ui);
            }

            return true;
        }

        public bool TrySelect(string name, string kind, out string? error)
        {
            error = ErrorCodes.NotFound;
            object? lockedHit = null;
            foreach (var ui in EquippableUi(kind))
            {
                if (!GameReflection.NamesEqual(DisplayName(ui), name))
                    continue;
                if (IsLocked(ui))
                {
                    lockedHit = ui;
                    continue;
                }

                if (TryPick(ui))
                {
                    error = null;
                    return true;
                }
            }

            if (TrySelectPerkItem(name, kind, out error))
                return true;

            if (lockedHit != null)
            {
                error = ErrorCodes.NotFound;
                return false;
            }

            error = error ?? ErrorCodes.NotFound;
            return false;
        }

        public bool TryStartLevel(string sceneName, out string? error)
        {
            error = ErrorCodes.UnsupportedInThisBuild;
            var player = GameReflection.Static("PlayerInteraction");
            if (player == null)
                return false;

            var interactor = FindLevelInteractor(sceneName);
            if (interactor == null)
            {
                error = ErrorCodes.NotFound;
                return false;
            }

            if (!CanBePlayed(interactor))
            {
                error = ErrorCodes.IllegalPhase;
                return false;
            }

            if (!BeginWithPlayer(interactor, player))
                return false;

            var manager = GameReflection.Static("LevelSelectManager");
            var play = manager?.GetType().GetMethod("PlayButtonPressed", Type.EmptyTypes);
            if (play == null)
                return false;
            play.Invoke(manager, null);
            error = null;
            return true;
        }

        static IEnumerable<object> EquippableUi(string kind)
        {
            foreach (var helper in GameReflection.Live("LoadoutUIHelper"))
            {
                if (helper == null)
                    continue;
                var lists = kind switch
                {
                    "weapon" => new[] { "weapons" },
                    "perk" => new[] { "perks" },
                    "mutator" => new[] { "mutators" },
                    _ => new[] { "perks", "weapons", "mutators" }
                };
                foreach (var listName in lists)
                {
                    foreach (var item in GameReflection.Enumerate(GameReflection.Read(helper, listName)))
                    {
                        if (KindMatches(item, kind))
                            yield return item;
                    }
                }
            }

            foreach (var ui in GameReflection.Live("TFUIEquippable"))
            {
                if (ui != null && KindMatches(ui, kind))
                    yield return ui;
            }
        }

        static bool TryPick(object ui)
        {
            var pick = ui.GetType().GetMethod("Pick", Type.EmptyTypes);
            if (pick == null)
                return false;
            pick.Invoke(ui, null);
            return true;
        }

        static bool TrySelectPerkItem(string name, string kind, out string? error)
        {
            error = ErrorCodes.NotFound;
            if (kind is not "" and not "perk")
                return false;

            object? item = null;
            foreach (var candidate in GameReflection.Live("PerkSelectionItem"))
            {
                if (candidate == null)
                    continue;
                var data = GameReflection.Read(candidate, "Equippable", "equippable");
                if (GameReflection.NamesEqual(GameReflection.Read(data, "displayName"), name) ||
                    GameReflection.NamesEqual(DisplayName(candidate), name))
                {
                    item = candidate;
                    break;
                }
            }

            if (item == null)
                return false;
            if (IsLocked(item) || IsLocked(GameReflection.Read(item, "Equippable", "equippable") ?? item))
                return false;

            var group = GameReflection.Read(item, "perkSelectionGroup")
                        ?? First(GameReflection.Live("PerkSelectionGroup"));
            if (group == null)
                return false;

            foreach (var method in group.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "SelectPerk")
                    continue;
                var args = method.GetParameters();
                if (args.Length != 1)
                    continue;
                if (args[0].ParameterType == typeof(string))
                    continue;
                if (!args[0].ParameterType.IsInstanceOfType(item))
                    continue;
                method.Invoke(group, new[] { item });
                error = null;
                return true;
            }

            return false;
        }

        static object? FindLevelInteractor(string sceneName)
        {
            var manager = GameReflection.Static("LevelSelectManager");
            if (manager != null)
            {
                foreach (var listName in new[] { "levelInteractors", "bonusLevelInteractors" })
                {
                    foreach (var item in GameReflection.Enumerate(GameReflection.Read(manager, listName)))
                    {
                        if (MatchesScene(item, sceneName))
                            return item;
                    }
                }
            }

            foreach (var item in GameReflection.Live("LevelInteractor"))
            {
                if (item != null && MatchesScene(item, sceneName))
                    return item;
            }

            return null;
        }

        static bool BeginWithPlayer(object interactor, object player)
        {
            MethodInfo? matched = null;
            foreach (var method in interactor.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "InteractionBegin")
                    continue;
                var args = method.GetParameters();
                if (args.Length != 1)
                    continue;
                if (!args[0].ParameterType.IsInstanceOfType(player))
                    continue;
                matched = method;
                break;
            }

            if (matched == null)
                return false;
            matched.Invoke(interactor, new[] { player });
            return true;
        }

        static bool MatchesScene(object interactor, string sceneName)
        {
            if (GameReflection.NamesEqual(GameReflection.Read(interactor, "sceneName", "SceneName"), sceneName))
                return true;
            var info = GameReflection.Read(interactor, "levelInfo", "LevelInfo");
            return GameReflection.NamesEqual(GameReflection.Read(info, "sceneName", "SceneName"), sceneName);
        }

        static bool CanBePlayed(object interactor) =>
            GameReflection.Read(interactor, "CanBePlayed") is not bool playable || playable;

        static object? DisplayName(object ui)
        {
            var data = GameReflection.Read(ui, "Data", "equippableData", "Equippable", "equippable") ?? ui;
            return GameReflection.Read(data, "displayName", "DisplayName");
        }

        static bool IsLocked(object ui)
        {
            if (GameReflection.Read(ui, "Locked") is bool locked)
                return locked;
            var data = GameReflection.Read(ui, "Data", "equippableData", "Equippable", "equippable");
            if (GameReflection.Read(data, "IsUnlocked") is bool unlocked)
                return !unlocked;
            if (GameReflection.Read(data, "Locked") is bool dataLocked)
                return dataLocked;
            return false;
        }

        static bool KindMatches(object ui, string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return true;
            var flag = kind switch
            {
                "perk" => GameReflection.Read(ui, "isPerk", "IsPerk"),
                "weapon" => GameReflection.Read(ui, "isWeapon", "IsWeapon"),
                "mutator" => GameReflection.Read(ui, "isMutator", "IsMutator"),
                _ => null
            };
            return flag is not bool b || b;
        }

        static object? First(object?[] items)
        {
            foreach (var item in items)
            {
                if (item != null)
                    return item;
            }

            return null;
        }
    }
}
