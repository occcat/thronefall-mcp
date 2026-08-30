using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

/// <summary>
/// Reflection over Unity / Assembly-CSharp so Game/* compiles against stub UnityEngine.
/// </summary>
static class UnityAccess
{
    const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags Declared = AnyInstance | BindingFlags.DeclaredOnly;

    static readonly Dictionary<string, Type?> Types = new(StringComparer.Ordinal);
    static readonly object Gate = new();
    static bool _scanned;
    static Assembly? _game;
    static Assembly? _unity;

    public static void Warmup()
    {
        lock (Gate)
        {
            EnsureAssemblies();
        }
    }

    public static Type? FindType(string name)
    {
        lock (Gate)
        {
            if (Types.TryGetValue(name, out var cached))
                return cached;
            EnsureAssemblies();
            var found = Lookup(name);
            Types[name] = found;
            return found;
        }
    }

    public static object? Singleton(string typeName, string member = "instance")
    {
        var t = FindType(typeName);
        if (t == null)
            return null;
        try
        {
            var p = t.GetProperty(member, AnyStatic);
            if (p != null)
                return Alive(p.GetValue(null));
            var f = t.GetField(member, AnyStatic);
            if (f != null)
                return Alive(f.GetValue(null));
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static object[] FindObjects(string typeName)
    {
        var t = FindType(typeName);
        var objType = FindType("UnityEngine.Object");
        if (t == null || objType == null)
            return Array.Empty<object>();
        try
        {
            var mi = objType.GetMethod("FindObjectsOfType", AnyStatic, null, new[] { typeof(Type) }, null);
            if (mi == null)
                return Array.Empty<object>();
            if (mi.Invoke(null, new object[] { t }) is not Array arr)
                return Array.Empty<object>();
            var list = new List<object>(arr.Length);
            for (var i = 0; i < arr.Length; i++)
            {
                var item = Alive(arr.GetValue(i));
                if (item != null)
                    list.Add(item);
            }

            return list.ToArray();
        }
        catch
        {
            return Array.Empty<object>();
        }
    }

    public static object? FindObject(string typeName)
    {
        var t = FindType(typeName);
        var objType = FindType("UnityEngine.Object");
        if (t == null || objType == null)
            return null;
        try
        {
            var mi = objType.GetMethod("FindObjectOfType", AnyStatic, null, new[] { typeof(Type) }, null);
            return Alive(mi?.Invoke(null, new object[] { t }));
        }
        catch
        {
            return null;
        }
    }

    public static int InstanceId(object obj)
    {
        var go = GameObjectOf(obj) ?? obj;
        try
        {
            var mi = go.GetType().GetMethod("GetInstanceID", Type.EmptyTypes);
            if (mi == null)
                mi = FindType("UnityEngine.Object")?.GetMethod("GetInstanceID", Type.EmptyTypes);
            if (mi == null)
                return 0;
            return Convert.ToInt32(mi.Invoke(go, null));
        }
        catch
        {
            return 0;
        }
    }

    public static object? GameObjectOf(object? obj)
    {
        if (obj == null)
            return null;
        if (obj.GetType().Name == "GameObject")
            return obj;
        return Get(obj, "gameObject") ?? obj;
    }

    public static string NameOf(object? obj)
    {
        var n = Get(obj, "name") as string;
        return n ?? "";
    }

    public static object? TransformOf(object? obj)
    {
        if (obj == null)
            return null;
        if (obj.GetType().Name == "Transform")
            return obj;
        return Get(obj, "transform");
    }

    public static Vec3Dto PositionOf(object? obj) => ToVec(Get(TransformOf(obj), "position"));

    public static object? Get(object? obj, string member)
    {
        if (obj == null)
            return null;
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var p = t.GetProperty(member, Declared);
                if (p != null)
                    return p.GetValue(obj);
                var f = t.GetField(member, Declared);
                if (f != null)
                    return f.GetValue(obj);
                t = t.BaseType;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static object? GetStatic(string typeName, string member)
    {
        var t = FindType(typeName);
        if (t == null)
            return null;
        try
        {
            var p = t.GetProperty(member, AnyStatic);
            if (p != null)
                return p.GetValue(null);
            var f = t.GetField(member, AnyStatic);
            if (f != null)
                return f.GetValue(null);
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static object? Call(object? obj, string method, params object?[] args)
    {
        if (obj == null)
            return null;
        try
        {
            var t = obj.GetType();
            MethodInfo? mi = null;
            while (t != null && t != typeof(object))
            {
                foreach (var cand in t.GetMethods(Declared))
                {
                    if (cand.Name != method)
                        continue;
                    if (cand.GetParameters().Length != args.Length)
                        continue;
                    mi = cand;
                    break;
                }

                if (mi != null)
                    break;
                t = t.BaseType;
            }

            return mi?.Invoke(obj, args);
        }
        catch
        {
            return null;
        }
    }

    public static object? CallStatic(string typeName, string method, params Type[] parameterTypes)
    {
        var t = FindType(typeName);
        if (t == null)
            return null;
        try
        {
            var mi = parameterTypes.Length == 0
                ? t.GetMethod(method, AnyStatic)
                : t.GetMethod(method, AnyStatic, null, parameterTypes, null);
            return mi?.Invoke(null, null);
        }
        catch
        {
            return null;
        }
    }

    public static object? GetComponent(object? host, string typeName)
    {
        if (host == null)
            return null;
        var t = FindType(typeName);
        if (t == null)
            return null;
        try
        {
            var mi = host.GetType().GetMethod("GetComponent", new[] { typeof(Type) });
            if (mi == null)
            {
                var tHost = host.GetType();
                while (tHost != null && mi == null)
                {
                    mi = tHost.GetMethod("GetComponent", Declared, null, new[] { typeof(Type) }, null);
                    tHost = tHost.BaseType;
                }
            }

            return Alive(mi?.Invoke(host, new object[] { t }));
        }
        catch
        {
            return null;
        }
    }

    public static object? GetComponentInChildren(object? host, string typeName)
    {
        if (host == null)
            return null;
        var t = FindType(typeName);
        if (t == null)
            return null;
        try
        {
            MethodInfo? mi = null;
            var tHost = host.GetType();
            while (tHost != null && mi == null)
            {
                mi = tHost.GetMethod("GetComponentInChildren", Declared, null, new[] { typeof(Type) }, null);
                tHost = tHost.BaseType;
            }

            return Alive(mi?.Invoke(host, new object[] { t }));
        }
        catch
        {
            return null;
        }
    }

    public static int Int(object? obj, string member, int fallback = 0)
    {
        var v = Get(obj, member);
        if (v == null)
            return fallback;
        try { return Convert.ToInt32(v); }
        catch { return fallback; }
    }

    public static float Float(object? obj, string member, float fallback = 0f)
    {
        var v = Get(obj, member);
        if (v == null)
            return fallback;
        try { return Convert.ToSingle(v); }
        catch { return fallback; }
    }

    public static bool Bool(object? obj, string member, bool fallback = false)
    {
        var v = Get(obj, member);
        if (v == null)
            return fallback;
        try { return Convert.ToBoolean(v); }
        catch { return fallback; }
    }

    public static string String(object? obj, string member, string fallback = "")
    {
        var v = Get(obj, member);
        return v?.ToString() ?? fallback;
    }

    public static string EnumName(object? obj, string member)
    {
        var v = Get(obj, member);
        return v?.ToString() ?? "";
    }

    public static Vec3Dto ToVec(object? value)
    {
        if (value == null)
            return new Vec3Dto();
        try
        {
            var t = value.GetType();
            var x = t.GetField("x")?.GetValue(value) ?? t.GetProperty("x")?.GetValue(value);
            var y = t.GetField("y")?.GetValue(value) ?? t.GetProperty("y")?.GetValue(value);
            var z = t.GetField("z")?.GetValue(value) ?? t.GetProperty("z")?.GetValue(value);
            return new Vec3Dto
            {
                X = x == null ? 0 : Convert.ToSingle(x),
                Y = y == null ? 0 : Convert.ToSingle(y),
                Z = z == null ? 0 : Convert.ToSingle(z)
            };
        }
        catch
        {
            return new Vec3Dto();
        }
    }

    public static IEnumerable<object> Enumerate(object? list)
    {
        if (list is not IEnumerable e)
            yield break;
        foreach (var item in e)
        {
            if (item != null)
                yield return item;
        }
    }

    public static int Count(object? list)
    {
        if (list == null)
            return 0;
        var n = Get(list, "Count") ?? Get(list, "Length");
        if (n != null)
        {
            try { return Convert.ToInt32(n); }
            catch { }
        }

        var c = 0;
        foreach (var _ in Enumerate(list))
            c++;
        return c;
    }

    public static object? Alive(object? obj)
    {
        if (obj == null)
            return null;
        try
        {
            var objType = FindType("UnityEngine.Object");
            if (objType != null && objType.IsInstanceOfType(obj))
            {
                var eq = objType.GetMethod("op_Equality", AnyStatic, null, new[] { objType, objType }, null);
                if (eq != null && eq.Invoke(null, new[] { obj, null }) is true)
                    return null;
            }

            return obj;
        }
        catch
        {
            return null;
        }
    }

    public static string ActiveSceneName()
    {
        try
        {
            var sm = FindType("UnityEngine.SceneManagement.SceneManager");
            var mi = sm?.GetMethod("GetActiveScene", AnyStatic);
            var scene = mi?.Invoke(null, null);
            return Get(scene, "name") as string ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static FieldInfo? FindField(string typeName, string field)
    {
        var t = FindType(typeName);
        while (t != null && t != typeof(object))
        {
            var f = t.GetField(field, Declared);
            if (f != null)
                return f;
            t = t.BaseType;
        }

        return null;
    }

    public static MethodInfo? FindMethod(string typeName, string method)
    {
        var t = FindType(typeName);
        while (t != null && t != typeof(object))
        {
            var m = t.GetMethod(method, Declared);
            if (m != null)
                return m;
            t = t.BaseType;
        }

        return null;
    }

    static void EnsureAssemblies()
    {
        if (_scanned)
            return;
        _scanned = true;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var n = asm.GetName().Name;
            if (n == "Assembly-CSharp")
                _game = asm;
            else if (n == "UnityEngine.CoreModule" || n == "UnityEngine")
                _unity ??= asm;
        }
    }

    static Type? Lookup(string name)
    {
        Type? t;
        if (_game != null)
        {
            t = _game.GetType(name) ?? _game.GetType("TagManager+" + name);
            if (t != null)
                return t;
        }

        if (_unity != null)
        {
            t = _unity.GetType(name);
            if (t != null)
                return t;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                t = asm.GetType(name);
                if (t != null)
                    return t;
            }
            catch
            {
                // skip
            }
        }

        return null;
    }
}
