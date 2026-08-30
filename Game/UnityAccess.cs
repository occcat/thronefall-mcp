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
    static readonly Dictionary<(Type Type, string Name), MemberInfo?> InstanceMembers = new();
    static readonly Dictionary<(Type Type, string Name), MemberInfo?> StaticMembers = new();
    static readonly Dictionary<(Type Type, string Name, int Argc), MethodInfo?> InstanceMethods = new();
    static readonly object Gate = new();
    static bool _scanned;
    static Assembly? _game;
    static Assembly? _unity;
    static MethodInfo? _findObjectsOfType;
    static MethodInfo? _findObjectOfType;

    [ThreadStatic]
    static RequestCache? _request;

    internal static bool TraceLookups = false;
    internal static int PropertyLookups = 0;
    internal static int MethodLookups = 0;
    internal static int FindObjectsLookups = 0;
    internal static int SingletonLookups = 0;

    internal static void ResetLookupTrace()
    {
        PropertyLookups = 0;
        MethodLookups = 0;
        FindObjectsLookups = 0;
        SingletonLookups = 0;
    }

    internal static IDisposable BeginRequestScope()
    {
        var previous = _request;
        _request = new RequestCache();
        return new ScopeReleaser(previous);
    }

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
        var cache = _request;
        var key = (typeName, member);
        if (cache != null && cache.Singletons.TryGetValue(key, out var hit))
            return hit;

        var found = ReadSingleton(typeName, member);
        if (cache != null)
            cache.Singletons[key] = found;
        return found;
    }

    public static object[] FindObjects(string typeName)
    {
        var cache = _request;
        if (cache != null && cache.Objects.TryGetValue(typeName, out var hit))
            return hit;

        var found = ScanObjects(typeName);
        if (cache != null)
            cache.Objects[typeName] = found;
        return found;
    }

    public static object? FindObject(string typeName)
    {
        var cache = _request;
        if (cache != null && cache.One.TryGetValue(typeName, out var hit))
            return hit;

        var found = ScanObject(typeName);
        if (cache != null)
            cache.One[typeName] = found;
        return found;
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
            var info = ResolveInstance(obj.GetType(), member);
            if (info is PropertyInfo p)
                return p.GetValue(obj);
            if (info is FieldInfo f)
                return f.GetValue(obj);
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
            var info = ResolveStatic(t, member);
            if (info is PropertyInfo p)
                return p.GetValue(null);
            if (info is FieldInfo f)
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
            var mi = ResolveMethod(obj.GetType(), method, args.Length);
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

    static object? ReadSingleton(string typeName, string member)
    {
        if (TraceLookups)
            SingletonLookups++;
        var t = FindType(typeName);
        if (t == null)
            return null;
        try
        {
            var info = ResolveStatic(t, member);
            if (info is PropertyInfo p)
                return Alive(p.GetValue(null));
            if (info is FieldInfo f)
                return Alive(f.GetValue(null));
        }
        catch
        {
            return null;
        }

        return null;
    }

    static object[] ScanObjects(string typeName)
    {
        if (TraceLookups)
            FindObjectsLookups++;
        var t = FindType(typeName);
        if (t == null)
            return Array.Empty<object>();
        var mi = FindObjectsOfTypeMethod();
        if (mi == null)
            return Array.Empty<object>();
        try
        {
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

    static object? ScanObject(string typeName)
    {
        var t = FindType(typeName);
        if (t == null)
            return null;
        var mi = FindObjectOfTypeMethod();
        if (mi == null)
            return null;
        try
        {
            return Alive(mi.Invoke(null, new object[] { t }));
        }
        catch
        {
            return null;
        }
    }

    static MethodInfo? FindObjectsOfTypeMethod()
    {
        if (_findObjectsOfType != null)
            return _findObjectsOfType;
        var objType = FindType("UnityEngine.Object");
        var mi = objType?.GetMethod("FindObjectsOfType", AnyStatic, null, new[] { typeof(Type) }, null);
        if (mi != null)
            _findObjectsOfType = mi;
        return mi;
    }

    static MethodInfo? FindObjectOfTypeMethod()
    {
        if (_findObjectOfType != null)
            return _findObjectOfType;
        var objType = FindType("UnityEngine.Object");
        var mi = objType?.GetMethod("FindObjectOfType", AnyStatic, null, new[] { typeof(Type) }, null);
        if (mi != null)
            _findObjectOfType = mi;
        return mi;
    }

    static MemberInfo? ResolveInstance(Type start, string name)
    {
        var key = (start, name);
        lock (Gate)
        {
            if (InstanceMembers.TryGetValue(key, out var cached))
                return cached;
        }

        MemberInfo? found = null;
        var t = start;
        while (t != null && t != typeof(object))
        {
            if (TraceLookups)
                PropertyLookups++;
            var p = t.GetProperty(name, Declared);
            if (p != null)
            {
                found = p;
                break;
            }

            var f = t.GetField(name, Declared);
            if (f != null)
            {
                found = f;
                break;
            }

            t = t.BaseType;
        }

        lock (Gate)
        {
            InstanceMembers[key] = found;
            return found;
        }
    }

    static MemberInfo? ResolveStatic(Type t, string name)
    {
        var key = (t, name);
        lock (Gate)
        {
            if (StaticMembers.TryGetValue(key, out var cached))
                return cached;
        }

        if (TraceLookups)
            PropertyLookups++;
        MemberInfo? found = (MemberInfo?)t.GetProperty(name, AnyStatic) ?? t.GetField(name, AnyStatic);
        lock (Gate)
        {
            StaticMembers[key] = found;
            return found;
        }
    }

    static MethodInfo? ResolveMethod(Type start, string name, int argc)
    {
        var key = (start, name, argc);
        lock (Gate)
        {
            if (InstanceMethods.TryGetValue(key, out var cached))
                return cached;
        }

        MethodInfo? found = null;
        var t = start;
        while (t != null && t != typeof(object))
        {
            if (TraceLookups)
                MethodLookups++;
            foreach (var cand in t.GetMethods(Declared))
            {
                if (cand.Name != name)
                    continue;
                if (cand.GetParameters().Length != argc)
                    continue;
                found = cand;
                break;
            }

            if (found != null)
                break;
            t = t.BaseType;
        }

        lock (Gate)
        {
            InstanceMethods[key] = found;
            return found;
        }
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

    sealed class RequestCache
    {
        public readonly Dictionary<(string Type, string Member), object?> Singletons = new();
        public readonly Dictionary<string, object[]> Objects = new(StringComparer.Ordinal);
        public readonly Dictionary<string, object?> One = new(StringComparer.Ordinal);
    }

    sealed class ScopeReleaser : IDisposable
    {
        readonly RequestCache? _previous;
        bool _disposed;

        public ScopeReleaser(RequestCache? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _request = _previous;
        }
    }
}
