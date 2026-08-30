using System;
using System.Globalization;
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
            _ = kind;
            var equippable = FindEquippable(name);
            if (equippable == null)
                return true;

            var prop = equippable.GetType().GetProperty("IsUnlocked")
                       ?? equippable.GetType().GetProperty("Locked");
            if (prop == null)
                return true;
            var value = prop.GetValue(equippable);
            if (prop.Name == "Locked" && value is bool locked)
                return !locked;
            return value is not bool b || b;
        }

        public bool TrySelect(string name, string kind, out string? error)
        {
            error = ErrorCodes.UnsupportedInThisBuild;
            var helper = Clr.Game("LoadoutUIHelper");
            if (helper != null)
            {
                foreach (var method in helper.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (method.Name != "TrySelectEquippableForLoadout")
                        continue;
                    var target = method.IsStatic ? null : Activator.CreateInstance(helper);
                    var args = BindSelectArgs(method, name, kind);
                    var invoked = method.Invoke(target, args);
                    if (invoked is bool ok)
                    {
                        if (ok)
                        {
                            error = null;
                            return true;
                        }

                        error = ErrorCodes.NotFound;
                        return false;
                    }

                    error = null;
                    return true;
                }
            }

            if (TryPerkSelect(name))
            {
                error = null;
                return true;
            }

            return false;
        }

        public bool TryStartLevel(string sceneName, out string? error)
        {
            error = ErrorCodes.UnsupportedInThisBuild;
            if (!TryBeginLevelInteractor(sceneName))
                return false;

            var managerType = Clr.Game("LevelSelectManager");
            var manager = managerType?.GetField("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var play = manager?.GetType().GetMethod("PlayButtonPressed", Type.EmptyTypes);
            if (play == null)
                return false;
            play.Invoke(manager, null);
            error = null;
            return true;
        }

        static bool TryPerkSelect(string name)
        {
            var groupType = Clr.Game("PerkSelectionGroup");
            if (groupType == null)
                return false;
            foreach (var method in groupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.Name != "SelectPerk")
                    continue;
                var args = method.GetParameters();
                if (args.Length == 1 && args[0].ParameterType == typeof(string))
                {
                    var target = method.IsStatic ? null : Activator.CreateInstance(groupType);
                    method.Invoke(target, new object[] { name });
                    return true;
                }
            }

            return false;
        }

        static bool TryBeginLevelInteractor(string sceneName)
        {
            var type = Clr.Game("LevelInteractor");
            var unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule")
                              ?? Type.GetType("UnityEngine.Object, UnityEngine");
            if (type == null || unityObject == null)
                return false;

            object? found = null;
            foreach (var method in unityObject.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "FindObjectsOfType")
                    continue;
                var parameters = method.GetParameters();
                try
                {
                    object? list;
                    if (method.IsGenericMethod && parameters.Length == 0)
                        list = method.MakeGenericMethod(type).Invoke(null, null);
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Type))
                        list = method.Invoke(null, new object[] { type });
                    else
                        continue;

                    if (list is not Array array)
                        continue;
                    foreach (var item in array)
                    {
                        if (item == null)
                            continue;
                        if (MatchesScene(item, sceneName) && CanBePlayed(item))
                        {
                            found = item;
                            break;
                        }
                    }

                    if (found != null)
                        break;
                }
                catch
                {
                    // next overload
                }
            }

            if (found == null)
                return false;
            var begin = found.GetType().GetMethod("InteractionBegin", Type.EmptyTypes);
            begin?.Invoke(found, null);
            return begin != null;
        }

        static bool MatchesScene(object interactor, string sceneName)
        {
            foreach (var member in new[] { "sceneName", "SceneName", "levelName", "LevelName" })
            {
                var value = interactor.GetType().GetField(member)?.GetValue(interactor)
                            ?? interactor.GetType().GetProperty(member)?.GetValue(interactor);
                if (value != null && string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), sceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            var info = interactor.GetType().GetProperty("LevelInfo")?.GetValue(interactor)
                       ?? interactor.GetType().GetField("levelInfo")?.GetValue(interactor);
            var infoName = info?.GetType().GetField("sceneName")?.GetValue(info)
                           ?? info?.GetType().GetProperty("sceneName")?.GetValue(info);
            return infoName != null &&
                   string.Equals(Convert.ToString(infoName, CultureInfo.InvariantCulture), sceneName, StringComparison.OrdinalIgnoreCase);
        }

        static bool CanBePlayed(object interactor)
        {
            var prop = interactor.GetType().GetProperty("CanBePlayed");
            if (prop?.GetValue(interactor) is bool b)
                return b;
            return true;
        }

        static object? FindEquippable(string name)
        {
            var type = Clr.Game("Equippable") ?? Clr.Game("TFUIEquippable");
            var unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule")
                              ?? Type.GetType("UnityEngine.Object, UnityEngine");
            if (type == null || unityObject == null)
                return null;
            try
            {
                var find = unityObject.GetMethod("FindObjectsOfType", new[] { typeof(Type) });
                if (find?.Invoke(null, new object[] { type }) is not Array array)
                    return null;
                foreach (var item in array)
                {
                    if (item == null)
                        continue;
                    var itemName = item.GetType().GetField("name")?.GetValue(item)
                                   ?? item.GetType().GetProperty("name")?.GetValue(item)
                                   ?? item.GetType().GetProperty("Name")?.GetValue(item);
                    if (itemName != null &&
                        string.Equals(Convert.ToString(itemName, CultureInfo.InvariantCulture), name, StringComparison.OrdinalIgnoreCase))
                        return item;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        static object?[] BindSelectArgs(MethodInfo method, string name, string kind)
        {
            var ps = method.GetParameters();
            var args = new object?[ps.Length];
            for (var i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(string))
                    args[i] = args[i] == null && Array.IndexOf(args, name) < 0 ? name : kind;
                else if (ps[i].ParameterType.IsValueType)
                    args[i] = Activator.CreateInstance(ps[i].ParameterType);
            }

            return args;
        }
    }
}
