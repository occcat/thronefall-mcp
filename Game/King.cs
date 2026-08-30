using System;
using System.Reflection;
using ThronefallControl.Config;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public interface IKingActions
{
    bool TeleportToCastle();
    bool TeleportToStart();
    bool TeleportTo(float x, float y, float z);
    bool ScriptedPosts();
}

public sealed class RecordingKingActions : IKingActions
{
    public int TeleportCastle { get; private set; }
    public int TeleportStart { get; private set; }
    public int TeleportCoords { get; private set; }
    public int ScriptedPostCalls { get; private set; }
    public int HoldCalls { get; private set; }
    public int InvulnerableCalls { get; private set; }

    public int CombatMutations =>
        TeleportCastle + TeleportStart + TeleportCoords + ScriptedPostCalls + HoldCalls + InvulnerableCalls;

    public bool TeleportToCastle()
    {
        TeleportCastle++;
        return true;
    }

    public bool TeleportToStart()
    {
        TeleportStart++;
        return true;
    }

    public bool TeleportTo(float x, float y, float z)
    {
        _ = (x, y, z);
        TeleportCoords++;
        return true;
    }

    public bool ScriptedPosts()
    {
        ScriptedPostCalls++;
        return true;
    }

    public void HoldNearCastle() => HoldCalls++;

    public void MakeInvulnerable() => InvulnerableCalls++;
}

public static class King
{
    public static IKingActions? Actions { get; set; }

    public static string CurrentPolicy { get; set; } = NightPolicies.Human;

    public static void Reset()
    {
        Actions = null;
        CurrentPolicy = string.IsNullOrEmpty(PluginConfig.DefaultNightPolicy)
            ? NightPolicies.Human
            : PluginConfig.DefaultNightPolicy;
    }

    public static NightPolicyResult ApplyPolicy(
        string? policy,
        bool dryRun = false,
        IKingActions? actions = null)
    {
        if (!NightPolicies.TryParse(policy, out var parsed))
        {
            return new NightPolicyResult
            {
                Ok = false,
                Error = ErrorCodes.NotFound,
                Message = $"unknown night policy '{policy}'",
                Policy = policy?.Trim() ?? ""
            };
        }

        var applied = new NightPolicyAppliedDto();
        var actor = actions ?? Actions;

        if (parsed == NightPolicies.Human)
        {
            // human: do not teleport, hold, command, or god-mode.
            if (!dryRun)
                CurrentPolicy = parsed;
            applied.Combat = CombatLabel(applied);
            return new NightPolicyResult
            {
                Ok = true,
                Policy = parsed,
                Applied = applied,
                DryRun = dryRun
            };
        }

        if (parsed == NightPolicies.AfkCastle)
        {
            applied.TeleportKing = true;
            if (!dryRun && actor != null && !actor.TeleportToCastle())
            {
                return UnsupportedPolicy(parsed, "king teleport to castle is unsupported in this build");
            }
        }
        else if (parsed == NightPolicies.ScriptedPosts)
        {
            applied.CommandUnits = true;
            if (!dryRun && actor != null && !actor.ScriptedPosts())
            {
                return UnsupportedPolicy(parsed, "scripted_posts dispatch is unsupported in this build");
            }
        }

        applied.Invulnerable = false;
        applied.Combat = CombatLabel(applied);
        if (!dryRun)
            CurrentPolicy = parsed;

        return new NightPolicyResult
        {
            Ok = true,
            Policy = parsed,
            Applied = applied,
            DryRun = dryRun
        };
    }

    public static KingTeleportResult Teleport(
        string? target,
        Vec3Dto? position,
        bool dryRun = false,
        IKingActions? actions = null)
    {
        var actor = actions ?? Actions;
        var kind = NormalizeTeleportTarget(target, position);
        if (kind == null)
        {
            return new KingTeleportResult
            {
                Ok = false,
                Error = ErrorCodes.NotFound,
                Message = "king teleport target must be castle, start, or coordinates"
            };
        }

        var result = new KingTeleportResult
        {
            Ok = true,
            Target = kind,
            Position = position ?? new Vec3Dto(),
            Invulnerable = false,
            DryRun = dryRun
        };

        if (dryRun)
            return result;

        var ok = kind switch
        {
            "castle" => actor == null || actor.TeleportToCastle(),
            "start" => actor == null || actor.TeleportToStart(),
            _ => actor == null || actor.TeleportTo(result.Position.X, result.Position.Y, result.Position.Z)
        };

        if (!ok)
        {
            result.Ok = false;
            result.Error = ErrorCodes.UnsupportedInThisBuild;
            result.Message = "king teleport is unsupported in this build";
            return result;
        }

        result.Teleported = true;
        return result;
    }

    static string CombatLabel(NightPolicyAppliedDto applied)
    {
        if (!applied.TeleportKing && !applied.ChangeHold && !applied.CommandUnits && !applied.Invulnerable)
            return "untouched";
        if (applied.CommandUnits)
            return NightPolicies.ScriptedPosts;
        if (applied.TeleportKing)
            return "king_to_castle";
        return "changed";
    }

    static NightPolicyResult UnsupportedPolicy(string policy, string message) =>
        new()
        {
            Ok = false,
            Error = ErrorCodes.UnsupportedInThisBuild,
            Message = message,
            Policy = policy
        };

    static string? NormalizeTeleportTarget(string? target, Vec3Dto? position)
    {
        if (string.IsNullOrWhiteSpace(target))
            return position == null ? null : "coords";

        switch (target.Trim().ToLowerInvariant())
        {
            case "castle":
                return "castle";
            case "start":
                return "start";
            case "coords":
            case "position":
            case "xyz":
                return position == null ? null : "coords";
            default:
                return position == null ? null : "coords";
        }
    }

    public sealed class ReflectionActions : IKingActions
    {
        public static ReflectionActions Instance { get; } = new();

        public bool TeleportToCastle()
        {
            var pos = FindTaggedPosition("CastleCenter") ?? FindTaggedPosition(4);
            if (pos == null)
                return false;
            return TeleportTo(pos.Value.x, pos.Value.y, pos.Value.z);
        }

        public bool TeleportToStart()
        {
            var movement = PlayerMovement();
            if (movement == null)
                return false;
            var method = movement.GetType().GetMethod("TeleportToStart", Type.EmptyTypes);
            if (method == null)
                return false;
            method.Invoke(movement, null);
            return true;
        }

        public bool TeleportTo(float x, float y, float z)
        {
            var movement = PlayerMovement();
            if (movement == null)
                return false;

            foreach (var method in movement.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "TeleportTo")
                    continue;
                var args = method.GetParameters();
                if (args.Length != 1)
                    continue;
                var vec = MakeVec3(args[0].ParameterType, x, y, z);
                if (vec == null)
                    continue;
                method.Invoke(movement, new[] { vec });
                return true;
            }

            return false;
        }

        public bool ScriptedPosts()
        {
            // Units worker owns spawn-line dispatch; king policy only records intent unless hooked.
            return true;
        }

        static object? PlayerMovement()
        {
            var type = Clr.Game("PlayerMovement");
            if (type == null)
                return null;
            return type.GetField("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        static (float x, float y, float z)? FindTaggedPosition(object tag)
        {
            var tmType = Clr.Game("TagManager");
            var tm = tmType?.GetField("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (tm == null || tmType == null)
                return null;

            foreach (var method in tmType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "FindClosestTaggedObjectWithTags")
                    continue;
                try
                {
                    var found = method.Invoke(tm, BuildTagArgs(method, tag));
                    var pos = ReadPosition(found);
                    if (pos != null)
                        return pos;
                }
                catch
                {
                    // try next overload
                }
            }

            return null;
        }

        static object?[]? BuildTagArgs(MethodInfo method, object tag)
        {
            var args = method.GetParameters();
            var values = new object?[args.Length];
            var filled = false;
            for (var i = 0; i < args.Length; i++)
            {
                var p = args[i].ParameterType;
                if (p.IsArray && !filled)
                {
                    var elem = p.GetElementType();
                    var arr = Array.CreateInstance(elem ?? typeof(object), 1);
                    arr.SetValue(CoerceTag(elem, tag), 0);
                    values[i] = arr;
                    filled = true;
                }
                else if (p.IsEnum && !filled)
                {
                    values[i] = CoerceTag(p, tag);
                    filled = true;
                }
                else if (p == typeof(string) && tag is string s && !filled)
                {
                    values[i] = s;
                    filled = true;
                }
                else if (p.IsValueType)
                {
                    values[i] = Activator.CreateInstance(p);
                }
            }

            return filled ? values : null;
        }

        static object CoerceTag(Type? enumType, object tag)
        {
            if (enumType == null || !enumType.IsEnum)
                return tag;
            if (tag is string name)
            {
                try { return Enum.Parse(enumType, name, ignoreCase: true); }
                catch { /* fall through */ }
            }

            try { return Enum.ToObject(enumType, tag); }
            catch { return tag; }
        }

        static (float x, float y, float z)? ReadPosition(object? obj)
        {
            if (obj == null)
                return null;
            var transform = obj.GetType().GetProperty("transform")?.GetValue(obj)
                            ?? obj.GetType().GetField("transform")?.GetValue(obj);
            var posObj = transform?.GetType().GetProperty("position")?.GetValue(transform);
            if (posObj == null)
                return null;
            var t = posObj.GetType();
            float Read(string n)
            {
                var p = t.GetField(n)?.GetValue(posObj) ?? t.GetProperty(n)?.GetValue(posObj);
                return p is float f ? f : Convert.ToSingle(p);
            }

            return (Read("x"), Read("y"), Read("z"));
        }

        static object? MakeVec3(Type type, float x, float y, float z)
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
    }
}

static class Clr
{
    public static Type? Game(string typeName) =>
        Type.GetType(typeName + ", Assembly-CSharp") ?? Type.GetType(typeName);
}
