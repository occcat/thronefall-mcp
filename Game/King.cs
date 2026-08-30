using System;
using System.Collections.Generic;
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
        GameReflection.Reset();
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
            applied.IntentOnly = true;
            applied.CommandUnits = false;
            applied.Invulnerable = false;
            applied.Combat = "intent_only";
            if (!dryRun)
                CurrentPolicy = parsed;
            return new NightPolicyResult
            {
                Ok = true,
                Policy = parsed,
                Applied = applied,
                DryRun = dryRun,
                Message = "scripted_posts is intent-only until the units worker owns spawn-line dispatch"
            };
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
                var vec = GameReflection.MakeVec3(args[0].ParameterType, x, y, z);
                if (vec == null)
                    continue;
                method.Invoke(movement, new[] { vec });
                return true;
            }

            return false;
        }

        public bool ScriptedPosts() => false;

        static object? PlayerMovement() => GameReflection.Static("PlayerMovement");

        static (float x, float y, float z)? FindTaggedPosition(object tag)
        {
            var tm = GameReflection.Static("TagManager");
            if (tm == null)
                return null;

            var etagType = GameReflection.Type("ETag") ?? GameReflection.Type("TagManager+ETag");
            var castle = CoerceTag(etagType, tag);

            var direct = FindDirectCastle(tm, etagType, castle);
            if (direct != null)
                return direct;

            return FindClosestCastle(tm, etagType, castle);
        }

        static (float x, float y, float z)? FindDirectCastle(object tm, Type? etagType, object castle)
        {
            foreach (var method in tm.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "FindAllTaggedObjectsWithTagDirect_UseWithCare")
                    continue;
                var args = method.GetParameters();
                if (args.Length != 1)
                    continue;
                object tagArg = castle;
                if (etagType != null && args[0].ParameterType.IsEnum)
                    tagArg = CoerceTag(args[0].ParameterType, castle);
                else if (!args[0].ParameterType.IsInstanceOfType(castle) && args[0].ParameterType.IsEnum)
                    tagArg = CoerceTag(args[0].ParameterType, castle);
                try
                {
                    var list = method.Invoke(tm, new[] { tagArg });
                    foreach (var item in GameReflection.Enumerate(list))
                    {
                        var pos = GameReflection.ReadPosition(item);
                        if (pos != null)
                            return pos;
                    }
                }
                catch
                {
                    // next overload
                }
            }

            return null;
        }

        static (float x, float y, float z)? FindClosestCastle(object tm, Type? etagType, object castle)
        {
            foreach (var method in tm.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "FindClosestTaggedObjectWithTags")
                    continue;
                var bound = BindClosestArgs(method, etagType, castle);
                if (bound == null)
                    continue;
                try
                {
                    var found = method.Invoke(tm, bound);
                    var pos = GameReflection.ReadPosition(found);
                    if (pos != null)
                        return pos;
                }
                catch
                {
                    // next overload
                }
            }

            return null;
        }

        static object[]? BindClosestArgs(MethodInfo method, Type? etagType, object castle)
        {
            var args = method.GetParameters();
            if (args.Length != 3)
                return null;
            if (!IsVec3(args[0].ParameterType))
                return null;
            if (!IsETagList(args[1].ParameterType, etagType) || !IsETagList(args[2].ParameterType, etagType))
                return null;

            var origin = Origin(args[0].ParameterType);
            if (origin == null)
                return null;
            var elem = args[1].ParameterType.IsGenericType
                ? args[1].ParameterType.GetGenericArguments()[0]
                : etagType ?? castle.GetType();
            var mustHave = GameReflection.MakeList(elem, CoerceTag(elem, castle));
            var mayNotHave = GameReflection.MakeList(elem);
            return new[] { origin, mustHave, mayNotHave };
        }

        static object? Origin(Type vecType)
        {
            var movement = PlayerMovement();
            var pos = GameReflection.ReadPosition(movement);
            if (pos != null)
            {
                var made = GameReflection.MakeVec3(vecType, pos.Value.x, pos.Value.y, pos.Value.z);
                if (made != null)
                    return made;
            }

            return GameReflection.MakeVec3(vecType, 0, 0, 0) ?? Activator.CreateInstance(vecType);
        }

        static bool IsVec3(Type type) =>
            type.Name == "Vector3" ||
            type.GetField("x") != null && type.GetField("y") != null && type.GetField("z") != null;

        static bool IsETagList(Type type, Type? etagType)
        {
            if (!type.IsGenericType)
                return false;
            if (type.GetGenericTypeDefinition() != typeof(List<>))
                return false;
            var elem = type.GetGenericArguments()[0];
            return elem.IsEnum || elem == etagType || elem.Name == "ETag";
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

            if (tag.GetType().IsEnum)
            {
                try { return Enum.ToObject(enumType, Convert.ToInt32(tag)); }
                catch { /* fall through */ }
            }

            try { return Enum.ToObject(enumType, tag); }
            catch { return tag; }
        }
    }
}
