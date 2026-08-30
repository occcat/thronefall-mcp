using System;
using System.Reflection;

namespace ThronefallControl.Game;

public static class ReflectionCache
{
    const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static object? _logger;

    public static Type? DayNightCycleType { get; private set; }
    public static PropertyInfo? DayNightCycleInstance { get; private set; }
    public static PropertyInfo? DayNightCycleCurrentTimestate { get; private set; }
    public static MethodInfo? DayNightCycleSwitchToNight { get; private set; }
    public static PropertyInfo? DayNightCycleCoinCountToBeHarvested { get; private set; }

    public static Type? PlayerInteractionType { get; private set; }
    public static FieldInfo? PlayerInteractionInstance { get; private set; }
    public static PropertyInfo? PlayerInteractionIsFreeToCallNight { get; private set; }
    public static PropertyInfo? PlayerInteractionBalance { get; private set; }

    public static Type? SceneTransitionManagerType { get; private set; }
    public static FieldInfo? SceneTransitionManagerInstance { get; private set; }
    public static PropertyInfo? SceneTransitionIsRunning { get; private set; }
    public static PropertyInfo? CurrentSceneState { get; private set; }
    public static MethodInfo? IsInLevelSelect { get; private set; }

    public static Type? CutOpenPathInteractorType { get; private set; }
    public static MethodInfo? ToggleCutPath { get; private set; }
    public static MethodInfo? IsToggleValidToUse { get; private set; }
    public static FieldInfo? PathOpened { get; private set; }
    public static FieldInfo? ToggleCost { get; private set; }
    public static FieldInfo? ToogleOnlyAtDay { get; private set; }
    public static FieldInfo? ToggleOnlyAtNight { get; private set; }
    public static PropertyInfo? CanBeInteractedWith { get; private set; }

    public static Type? UnityObjectType { get; private set; }

    public static bool CutOpenPathToggleBound => ToggleCutPath != null;

    public static void TryInit(object? logger = null)
    {
        _logger = logger;

        DayNightCycleType = FindType("DayNightCycle");
        DayNightCycleInstance = GetProperty(DayNightCycleType, "Instance", StaticAny);
        DayNightCycleCurrentTimestate = GetProperty(DayNightCycleType, "CurrentTimestate", InstanceAny);
        DayNightCycleSwitchToNight = GetMethod(DayNightCycleType, "SwitchToNight", InstanceAny);
        DayNightCycleCoinCountToBeHarvested = GetProperty(DayNightCycleType, "CoinCountToBeHarvested", InstanceAny);

        PlayerInteractionType = FindType("PlayerInteraction");
        PlayerInteractionInstance = GetField(PlayerInteractionType, "instance", StaticAny);
        PlayerInteractionIsFreeToCallNight = GetProperty(PlayerInteractionType, "IsFreeToCallNight", InstanceAny);
        PlayerInteractionBalance = GetProperty(PlayerInteractionType, "Balance", InstanceAny);

        SceneTransitionManagerType = FindType("SceneTransitionManager");
        SceneTransitionManagerInstance = GetField(SceneTransitionManagerType, "instance", StaticAny);
        SceneTransitionIsRunning = GetProperty(SceneTransitionManagerType, "SceneTransitionIsRunning", InstanceAny);
        CurrentSceneState = GetProperty(SceneTransitionManagerType, "CurrentSceneState", InstanceAny);
        IsInLevelSelect = GetMethod(SceneTransitionManagerType, "IsInLevelSelect", InstanceAny);

        CutOpenPathInteractorType = FindType("CutOpenPathInteractor");
        ToggleCutPath = GetMethod(CutOpenPathInteractorType, "ToggleCutPath", InstanceAny);
        IsToggleValidToUse = GetMethod(CutOpenPathInteractorType, "IsToggleValidToUse", InstanceAny);
        PathOpened = GetField(CutOpenPathInteractorType, "pathOpened", InstanceAny);
        ToggleCost = GetField(CutOpenPathInteractorType, "toggleCost", InstanceAny);
        ToogleOnlyAtDay = GetField(CutOpenPathInteractorType, "toogleOnlyAtDay", InstanceAny);
        ToggleOnlyAtNight = GetField(CutOpenPathInteractorType, "toggleOnlyAtNight", InstanceAny);
        CanBeInteractedWith = GetProperty(CutOpenPathInteractorType, "CanBeInteractedWith", InstanceAny);

        UnityObjectType = FindType("UnityEngine.Object")
            ?? Type.GetType("UnityEngine.Object, UnityEngine.CoreModule")
            ?? Type.GetType("UnityEngine.Object, UnityEngine");
    }

    public static object? GetDayNightCycle() =>
        DayNightCycleInstance?.GetValue(null);

    public static object? GetPlayerInteraction() =>
        PlayerInteractionInstance?.GetValue(null);

    public static object? GetSceneTransitionManager() =>
        SceneTransitionManagerInstance?.GetValue(null);

    public static object[] FindCutters()
    {
        if (CutOpenPathInteractorType == null)
            return Array.Empty<object>();
        return FindObjectsOfType(CutOpenPathInteractorType);
    }

    public static object[] FindObjectsOfType(Type type)
    {
        if (UnityObjectType == null || type == null)
            return Array.Empty<object>();

        foreach (var method in UnityObjectType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "FindObjectsOfType" || method.IsGenericMethod)
                continue;
            var ps = method.GetParameters();
            object? raw = null;
            try
            {
                if (ps.Length == 1 && ps[0].ParameterType == typeof(Type))
                    raw = method.Invoke(null, new object[] { type });
                else if (ps.Length == 2 && ps[0].ParameterType == typeof(Type) && ps[1].ParameterType == typeof(bool))
                    raw = method.Invoke(null, new object[] { type, true });
            }
            catch
            {
                continue;
            }

            if (raw is Array arr)
            {
                var list = new object[arr.Length];
                arr.CopyTo(list, 0);
                return list;
            }
        }

        return Array.Empty<object>();
    }

    public static int GetGameObjectInstanceId(object obj)
    {
        if (obj == null)
            return 0;
        var go = obj.GetType().GetProperty("gameObject", InstanceAny)?.GetValue(obj) ?? obj;
        var method = go.GetType().GetMethod("GetInstanceID", Type.EmptyTypes);
        if (method?.Invoke(go, null) is int id)
            return id;
        return 0;
    }

    public static string GetObjectName(object obj)
    {
        if (obj == null)
            return "";
        var go = obj.GetType().GetProperty("gameObject", InstanceAny)?.GetValue(obj) ?? obj;
        if (go.GetType().GetProperty("name", InstanceAny)?.GetValue(go) is string name)
            return name;
        return obj.GetType().Name;
    }

    public static bool ReadBool(PropertyInfo? prop, object target, bool fallback = false)
    {
        if (prop == null || target == null)
            return fallback;
        try
        {
            return prop.GetValue(target) is bool b && b;
        }
        catch
        {
            return fallback;
        }
    }

    public static bool ReadBool(FieldInfo? field, object target, bool fallback = false)
    {
        if (field == null || target == null)
            return fallback;
        try
        {
            return field.GetValue(target) is bool b && b;
        }
        catch
        {
            return fallback;
        }
    }

    public static int ReadInt(PropertyInfo? prop, object target, int fallback = 0)
    {
        if (prop == null || target == null)
            return fallback;
        try
        {
            return ToInt(prop.GetValue(target), fallback);
        }
        catch
        {
            return fallback;
        }
    }

    public static int ReadInt(FieldInfo? field, object target, int fallback = 0)
    {
        if (field == null || target == null)
            return fallback;
        try
        {
            return ToInt(field.GetValue(target), fallback);
        }
        catch
        {
            return fallback;
        }
    }

    public static string ReadName(PropertyInfo? prop, object target)
    {
        if (prop == null || target == null)
            return "";
        try
        {
            var value = prop.GetValue(target);
            return value?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static bool InvokeBool(MethodInfo? method, object target, bool fallback = false)
    {
        if (method == null || target == null)
            return fallback;
        try
        {
            return method.Invoke(target, null) is bool b && b;
        }
        catch
        {
            return fallback;
        }
    }

    public static void Invoke(MethodInfo? method, object target)
    {
        method?.Invoke(target, null);
    }

    static int ToInt(object? value, int fallback)
    {
        switch (value)
        {
            case int i:
                return i;
            case float f:
                return (int)f;
            case double d:
                return (int)d;
            case null:
                return fallback;
            default:
                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    return fallback;
                }
        }
    }

    static Type? FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = null;
            try
            {
                type = asm.GetType(name, throwOnError: false);
            }
            catch
            {
                type = null;
            }

            if (type != null)
                return type;

            if (asm.GetName().Name == "Assembly-CSharp")
            {
                try
                {
                    type = asm.GetType(name) ?? asm.GetType("." + name);
                }
                catch
                {
                    type = null;
                }

                if (type != null)
                    return type;
            }
        }

        return Type.GetType(name, throwOnError: false);
    }

    static PropertyInfo? GetProperty(Type? type, string name, BindingFlags flags)
    {
        var prop = type?.GetProperty(name, flags);
        if (type != null && prop == null)
            Warn($"{type.Name}.{name} property missing");
        return prop;
    }

    static FieldInfo? GetField(Type? type, string name, BindingFlags flags)
    {
        var field = type?.GetField(name, flags);
        if (type != null && field == null)
            Warn($"{type.Name}.{name} field missing");
        return field;
    }

    static MethodInfo? GetMethod(Type? type, string name, BindingFlags flags)
    {
        var method = type?.GetMethod(name, flags, binder: null, types: Type.EmptyTypes, modifiers: null)
            ?? type?.GetMethod(name, flags);
        if (type != null && method == null)
            Warn($"{type.Name}.{name} method missing");
        return method;
    }

    static void Warn(string message)
    {
        if (_logger == null)
            return;
        var method = _logger.GetType().GetMethod("LogWarning", new[] { typeof(object) })
            ?? _logger.GetType().GetMethod("LogWarning", new[] { typeof(string) });
        method?.Invoke(_logger, new object[] { "ReflectionCache: " + message });
    }
}
