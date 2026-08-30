using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace ThronefallControl.Game;

public static class GameReflection
{
    public static Func<string, Type?>? Types { get; set; }
    public static Func<Type, object?[]>? LiveObjects { get; set; }

    public static void Reset()
    {
        Types = null;
        LiveObjects = null;
    }

    public static Type? Type(string name)
    {
        if (Types != null)
            return Types(name);
        return System.Type.GetType(name + ", Assembly-CSharp")
               ?? System.Type.GetType(name);
    }

    public static object? Static(string typeName, string member = "instance")
    {
        var type = Type(typeName);
        if (type == null)
            return null;
        return type.GetField(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
               ?? type.GetProperty(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    public static object?[] Live(string typeName)
    {
        var type = Type(typeName);
        return type == null ? Array.Empty<object?>() : Live(type);
    }

    public static object?[] Live(Type type)
    {
        if (LiveObjects != null)
            return LiveObjects(type) ?? Array.Empty<object?>();

        var unity = System.Type.GetType("UnityEngine.Object, UnityEngine.CoreModule")
                    ?? System.Type.GetType("UnityEngine.Object, UnityEngine");
        if (unity == null)
            return Array.Empty<object?>();

        foreach (var method in unity.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "FindObjectsOfType")
                continue;
            try
            {
                object? list;
                var parameters = method.GetParameters();
                if (method.IsGenericMethod && parameters.Length == 0)
                    list = method.MakeGenericMethod(type).Invoke(null, null);
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Type))
                    list = method.Invoke(null, new object[] { type });
                else
                    continue;
                return ToArray(list);
            }
            catch
            {
                // next overload
            }
        }

        return Array.Empty<object?>();
    }

    public static object? Read(object? target, string name)
    {
        if (target == null)
            return null;
        var type = target.GetType();
        return type.GetProperty(name)?.GetValue(target)
               ?? type.GetField(name)?.GetValue(target);
    }

    public static object? Read(object? target, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Read(target, name);
            if (value != null)
                return value;
        }

        return null;
    }

    public static IEnumerable<object> Enumerate(object? value)
    {
        if (value == null || value is string)
            yield break;
        if (value is IEnumerable sequence)
        {
            foreach (var item in sequence)
            {
                if (item != null)
                    yield return item;
            }

            yield break;
        }

        yield return value;
    }

    public static object MakeList(Type elementType, params object[] items)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = Activator.CreateInstance(listType)!;
        var add = listType.GetMethod("Add", new[] { elementType });
        if (add != null)
        {
            foreach (var item in items)
                add.Invoke(list, new[] { item });
        }

        return list;
    }

    public static object? MakeVec3(Type type, float x, float y, float z)
    {
        try
        {
            var boxed = Activator.CreateInstance(type);
            if (boxed == null)
                return null;
            SetFloat(type, boxed, "x", x);
            SetFloat(type, boxed, "y", y);
            SetFloat(type, boxed, "z", z);
            return boxed;
        }
        catch
        {
            return null;
        }
    }

    public static (float x, float y, float z)? ReadPosition(object? obj)
    {
        if (obj == null)
            return null;
        var transform = Read(obj, "transform");
        var posObj = Read(transform, "position") ?? Read(obj, "position");
        if (posObj == null)
            return null;
        return (ReadFloat(posObj, "x"), ReadFloat(posObj, "y"), ReadFloat(posObj, "z"));
    }

    public static bool NamesEqual(object? value, string expected) =>
        value != null &&
        string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), expected, StringComparison.OrdinalIgnoreCase);

    static float ReadFloat(object obj, string name)
    {
        var value = Read(obj, name);
        return value is float f ? f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }

    static void SetFloat(Type type, object boxed, string name, float value)
    {
        var field = type.GetField(name);
        if (field != null)
        {
            field.SetValue(boxed, value);
            return;
        }

        type.GetProperty(name)?.SetValue(boxed, value);
    }

    static object?[] ToArray(object? list)
    {
        var items = new List<object?>();
        foreach (var item in Enumerate(list))
            items.Add(item);
        return items.ToArray();
    }
}