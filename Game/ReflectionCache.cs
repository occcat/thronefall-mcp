using System;
using System.Reflection;

namespace ThronefallControl.Game;

public static class ReflectionCache
{
    const BindingFlags Any =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool Initialized { get; private set; }
    public static bool PathfindReady { get; private set; }
    public static bool CommandUnitsReady { get; private set; }
    public static bool GroupsReady { get; private set; }
    public static bool SpawnsReady { get; private set; }
    public static bool SlotsReady { get; private set; }

    public static Type? CommandUnitsType { get; private set; }
    public static FieldInfo? CommandUnitsInstance { get; private set; }
    public static FieldInfo? PlayerUnitsCommanding { get; private set; }
    public static FieldInfo? PlayerUnitsCommandingBuffer { get; private set; }
    public static FieldInfo? ToBePlaced { get; private set; }
    public static FieldInfo? Commanding { get; private set; }
    public static MethodInfo? PlaceCommandedUnits { get; private set; }
    public static MethodInfo? MakeUnitsInBufferHoldPosition { get; private set; }
    public static MethodInfo? ForceCommandingEnd { get; private set; }
    public static MethodInfo? AddUnitsToGroup { get; private set; }
    public static MethodInfo? RemoveAllUnitsfromAllGroups { get; private set; }
    public static MethodInfo? TryToSelectUnits { get; private set; }
    public static MethodInfo? OnUnitAdd { get; private set; }

    public static Type? PathfindMovementPlayerunitType { get; private set; }
    public static PropertyInfo? HomePosition { get; private set; }
    public static PropertyInfo? HoldPosition { get; private set; }
    public static PropertyInfo? FollowingPlayer { get; private set; }
    public static PropertyInfo? Flying { get; private set; }
    public static MethodInfo? FollowPlayer { get; private set; }
    public static MethodInfo? SnapToNavmesh { get; private set; }
    public static MethodInfo? GetNearestGroundPosition { get; private set; }

    public static Type? TagManagerType { get; private set; }
    public static Type? ETagType { get; private set; }
    public static FieldInfo? TagManagerInstance { get; private set; }
    public static PropertyInfo? PlayerUnits { get; private set; }
    public static MethodInfo? FindClosestTaggedObjectWithTags { get; private set; }
    public static MethodInfo? FindUnsortedTaggedObjectsInRange { get; private set; }

    public static Type? TaggedObjectType { get; private set; }
    public static PropertyInfo? TaggedObjectTags { get; private set; }
    public static PropertyInfo? TaggedObjectHp { get; private set; }
    public static MethodInfo? AddUnitGroupTag { get; private set; }
    public static MethodInfo? RemoveUnitGroupTag { get; private set; }
    public static MethodInfo? TaggedObjectContains { get; private set; }

    public static Type? EnemySpawnLineType { get; private set; }
    public static PropertyInfo? SpawnLine { get; private set; }

    public static Type? SceneTransitionManagerType { get; private set; }
    public static FieldInfo? SceneTransitionManagerInstance { get; private set; }
    public static PropertyInfo? SceneTransitionIsRunning { get; private set; }
    public static PropertyInfo? CurrentSceneState { get; private set; }

    public static Type? DayNightCycleType { get; private set; }
    public static Type? TimestateType { get; private set; }
    public static PropertyInfo? DayNightCycleInstance { get; private set; }
    public static PropertyInfo? CurrentTimestate { get; private set; }

    public static Type? Vector3Type { get; private set; }
    public static Type? TransformType { get; private set; }
    public static Type? UnityObjectType { get; private set; }
    public static Type? GameObjectType { get; private set; }
    public static Type? ComponentType { get; private set; }

    public static int ETagCastleCenter { get; private set; } = 4;
    public static int ETagWall { get; private set; } = 13;
    public static int ETagGroup1 { get; private set; } = 45;
    public static int ETagGroup2 { get; private set; } = 46;
    public static int ETagGroup3 { get; private set; } = 47;

    public static Type? TagManager => TagManagerType;
    public static Type? DayNightCycle => DayNightCycleType;
    public static Type? SceneTransitionManager => SceneTransitionManagerType;
    public static Type? UnityObject => UnityObjectType;
    public static FieldInfo? SceneTransitionInstance => SceneTransitionManagerInstance;
    public static PropertyInfo? DayNightInstance => DayNightCycleInstance;

    public static Type? BuildSlot { get; private set; }
    public static Type? BuildingInteractor { get; private set; }
    public static Type? PlayerInteraction { get; private set; }
    public static Type? PlayerMovement { get; private set; }
    public static Type? ChoiceManager { get; private set; }
    public static Type? Choice { get; private set; }
    public static Type? SceneManager { get; private set; }

    public static MethodInfo? TryToBuildOrUpgradeAndPay { get; private set; }
    public static MethodInfo? ExecuteBuildOrUpgrade { get; private set; }
    public static MethodInfo? OnUpgradeChoiceComplete { get; private set; }
    public static MethodInfo? Harvest { get; private set; }
    public static MethodInfo? MarkAsHarvested { get; private set; }
    public static MethodInfo? SpendCoins { get; private set; }
    public static MethodInfo? SpendEnergyCores { get; private set; }
    public static MethodInfo? TeleportTo { get; private set; }
    public static MethodInfo? GetInstanceId { get; private set; }
    public static MethodInfo? GetActiveScene { get; private set; }

    public static PropertyInfo? BuildSlotLevel { get; private set; }
    public static PropertyInfo? CanBeUpgraded { get; private set; }
    public static PropertyInfo? NextUpgradeIsChoice { get; private set; }
    public static PropertyInfo? NextUpgradeOrBuildCost { get; private set; }
    public static PropertyInfo? NextUpgradeOrBuildEnergyCoreCost { get; private set; }
    public static PropertyInfo? GoldIncome { get; private set; }
    public static PropertyInfo? EnergyCoreIncome { get; private set; }
    public static PropertyInfo? BuildSlotUpgrades { get; private set; }
    public static PropertyInfo? BuildSlotInteractor { get; private set; }
    public static PropertyInfo? CanBeHarvested { get; private set; }
    public static PropertyInfo? IsWaitingForChoice { get; private set; }
    public static PropertyInfo? KnockedOutTonight { get; private set; }
    public static PropertyInfo? Balance { get; private set; }
    public static PropertyInfo? EnergyCoreBalance { get; private set; }
    public static PropertyInfo? TrueBalance { get; private set; }
    public static PropertyInfo? Transform { get; private set; }
    public static PropertyInfo? Position { get; private set; }
    public static PropertyInfo? GameObject { get; private set; }
    public static PropertyInfo? ChoiceCanBePicked { get; private set; }
    public static PropertyInfo? ChoiceCoroutineRunning { get; private set; }

    public static FieldInfo? BuildingName { get; private set; }
    public static FieldInfo? TargetBuilding { get; private set; }
    public static FieldInfo? BuildingInteractorOnSlot { get; private set; }
    public static FieldInfo? HarvestedToday { get; private set; }
    public static FieldInfo? WaitingForChoiceField { get; private set; }
    public static FieldInfo? PlayerBuildingInteractors { get; private set; }
    public static FieldInfo? PlayerInteractionInstance { get; private set; }
    public static FieldInfo? PlayerMovementInstance { get; private set; }
    public static FieldInfo? ChoiceManagerInstance { get; private set; }
    public static FieldInfo? AvailableChoices { get; private set; }
    public static FieldInfo? ChoiceToReturn { get; private set; }
    public static FieldInfo? CurrentOriginBuildSlot { get; private set; }
    public static FieldInfo? ChoiceName { get; private set; }
    public static FieldInfo? ChoiceTooltip { get; private set; }
    public static FieldInfo? UpgradeBranches { get; private set; }
    public static FieldInfo? ChoiceDetails { get; private set; }

    public static void TryInit(object? logger = null)
    {
        try
        {
            UnityAccess.Warmup();
            _ = UnityAccess.FindField("BuildingInteractor", "harvestedToday");
            _ = UnityAccess.FindField("BuildSlot", "requiredRoot");
            _ = UnityAccess.FindField("CutOpenPathInteractor", "pathOpened");
            _ = UnityAccess.FindField("CutOpenPathInteractor", "toggleCost");
            _ = UnityAccess.FindMethod("CutOpenPathInteractor", "IsToggleValidToUse");
            _ = UnityAccess.FindField("MatchSave", "currentLoadoutAsString");
            _ = UnityAccess.FindField("EnemySpawnLine", "difficulty");
        }
        catch
        {
            // missing private members must not prevent plugin start
        }

        try
        {
            Bind(logger);
        }
        catch (Exception ex)
        {
            Warn(logger, "ReflectionCache.TryInit failed: " + ex.Message);
        }

        try
        {
            BindSlots(logger);
        }
        catch (Exception ex)
        {
            SlotsReady = false;
            Warn(logger, "slot reflection init failed: " + ex.Message);
        }

        Initialized = true;
    }

    static void Bind(object? logger)
    {
        CommandUnitsType = FindType("CommandUnits");
        PathfindMovementPlayerunitType = FindType("PathfindMovementPlayerunit");
        TagManagerType = FindType("TagManager");
        ETagType = FindType("TagManager+ETag") ?? FindNested(TagManagerType, "ETag");
        TaggedObjectType = FindType("TaggedObject");
        EnemySpawnLineType = FindType("EnemySpawnLine");
        SceneTransitionManagerType = FindType("SceneTransitionManager");
        DayNightCycleType = FindType("DayNightCycle");
        TimestateType = FindType("DayNightCycle+Timestate") ?? FindNested(DayNightCycleType, "Timestate");
        Vector3Type = FindType("UnityEngine.Vector3");
        TransformType = FindType("UnityEngine.Transform");
        UnityObjectType = FindType("UnityEngine.Object");
        GameObjectType = FindType("UnityEngine.GameObject");
        ComponentType = FindType("UnityEngine.Component");

        CommandUnitsInstance = F(CommandUnitsType, "instance");
        PlayerUnitsCommanding = F(CommandUnitsType, "playerUnitsCommanding");
        PlayerUnitsCommandingBuffer = F(CommandUnitsType, "playerUnitsCommandingBuffer");
        ToBePlaced = F(CommandUnitsType, "toBePlaced");
        Commanding = F(CommandUnitsType, "commanding");
        PlaceCommandedUnits = M(CommandUnitsType, "PlaceCommandedUnitsAndCalculateTargetPositions", 1);
        MakeUnitsInBufferHoldPosition = M(CommandUnitsType, "MakeUnitsInBufferHoldPosition", 0);
        ForceCommandingEnd = M(CommandUnitsType, "ForceCommandingEnd", 0);
        AddUnitsToGroup = M(CommandUnitsType, "AddUnitsToGroup", 1);
        RemoveAllUnitsfromAllGroups = M(CommandUnitsType, "RemoveAllUnitsfromAllGroups", 0);
        TryToSelectUnits = M(CommandUnitsType, "TryToSelectUnits", 1);
        OnUnitAdd = M(CommandUnitsType, "OnUnitAdd", 2);

        HomePosition = P(PathfindMovementPlayerunitType, "HomePosition");
        HoldPosition = P(PathfindMovementPlayerunitType, "HoldPosition");
        FollowingPlayer = P(PathfindMovementPlayerunitType, "FollowingPlayer");
        Flying = P(PathfindMovementPlayerunitType, "Flying");
        FollowPlayer = M(PathfindMovementPlayerunitType, "FollowPlayer", 1);
        SnapToNavmesh = M(PathfindMovementPlayerunitType, "SnapToNavmesh", 0);
        GetNearestGroundPosition = M(PathfindMovementPlayerunitType, "GetNearestGroundPosition", 1);

        TagManagerInstance = F(TagManagerType, "instance");
        PlayerUnits = P(TagManagerType, "PlayerUnits");
        FindClosestTaggedObjectWithTags = M(TagManagerType, "FindClosestTaggedObjectWithTags", 3);
        FindUnsortedTaggedObjectsInRange = M(TagManagerType, "FindUnsortedTaggedObjectsInRange", 4);

        TaggedObjectTags = P(TaggedObjectType, "Tags");
        TaggedObjectHp = P(TaggedObjectType, "Hp");
        AddUnitGroupTag = M(TaggedObjectType, "AddUnitGroupTag", 1);
        RemoveUnitGroupTag = M(TaggedObjectType, "RemoveUnitGroupTag", 1);
        TaggedObjectContains = M(TaggedObjectType, "Contains", 1);

        SpawnLine = P(EnemySpawnLineType, "SpawnLine");

        SceneTransitionManagerInstance = F(SceneTransitionManagerType, "instance");
        SceneTransitionIsRunning = P(SceneTransitionManagerType, "SceneTransitionIsRunning");
        CurrentSceneState = P(SceneTransitionManagerType, "CurrentSceneState")
                            ?? P(SceneTransitionManagerType, "SceneState");

        DayNightCycleInstance = P(DayNightCycleType, "Instance") ?? P(DayNightCycleType, "instance");
        CurrentTimestate = P(DayNightCycleType, "CurrentTimestate");

        ETagCastleCenter = EnumInt(ETagType, "CastleCenter", 4);
        ETagWall = EnumInt(ETagType, "Wall", 13);
        ETagGroup1 = EnumInt(ETagType, "Group1", 45);
        ETagGroup2 = EnumInt(ETagType, "Group2", 46);
        ETagGroup3 = EnumInt(ETagType, "Group3", 47);

        PathfindReady = HomePosition?.GetSetMethod(true) != null && HoldPosition?.GetSetMethod(true) != null;
        CommandUnitsReady = CommandUnitsInstance != null
                            && PlaceCommandedUnits != null
                            && (PlayerUnitsCommanding != null || OnUnitAdd != null);
        GroupsReady = AddUnitGroupTag != null && RemoveUnitGroupTag != null;
        SpawnsReady = EnemySpawnLineType != null && SpawnLine != null;

        if (!PathfindReady)
            Warn(logger, "PathfindMovementPlayerunit HomePosition/HoldPosition missing; unit fallback disabled");
        if (CommandUnitsType != null && !CommandUnitsReady)
            Warn(logger, "CommandUnits solver members missing; UseCommandUnitsSolver will fall back");
        if (TryToSelectUnits == null && CommandUnitsType != null)
            Warn(logger, "CommandUnits.TryToSelectUnits not found (unused; buffer is filled directly)");
    }

    static void BindSlots(object? logger)
    {
        BuildSlot = FindType("BuildSlot");
        BuildingInteractor = FindType("BuildingInteractor");
        PlayerInteraction = FindType("PlayerInteraction");
        PlayerMovement = FindType("PlayerMovement");
        ChoiceManager = FindType("ChoiceManager");
        Choice = FindType("Choice");
        SceneManager = FindType("UnityEngine.SceneManagement.SceneManager");

        TryToBuildOrUpgradeAndPay = M(BuildSlot, "TryToBuildOrUpgradeAndPay", -1);
        ExecuteBuildOrUpgrade = M(BuildSlot, "ExecuteBuildOrUpgrade", -1);
        OnUpgradeChoiceComplete = M(BuildSlot, "OnUpgradeChoiceComplete", -1);
        Harvest = M(BuildingInteractor, "Harvest", -1);
        MarkAsHarvested = M(BuildingInteractor, "MarkAsHarvested", -1);
        SpendCoins = M(PlayerInteraction, "SpendCoins", -1);
        SpendEnergyCores = M(PlayerInteraction, "SpendEnergyCores", -1);
        TeleportTo = M(PlayerMovement, "TeleportTo", -1);
        GetInstanceId = M(UnityObjectType, "GetInstanceID", 0) ?? M(GameObjectType, "GetInstanceID", 0);
        GetActiveScene = M(SceneManager, "GetActiveScene", 0);

        BuildSlotLevel = P(BuildSlot, "Level");
        CanBeUpgraded = P(BuildSlot, "CanBeUpgraded");
        NextUpgradeIsChoice = P(BuildSlot, "NextUpgradeIsChoice");
        NextUpgradeOrBuildCost = P(BuildSlot, "NextUpgradeOrBuildCost");
        NextUpgradeOrBuildEnergyCoreCost = P(BuildSlot, "NextUpgradeOrBuildEnergyCoreCost");
        GoldIncome = P(BuildSlot, "GoldIncome");
        EnergyCoreIncome = P(BuildSlot, "EnergyCoreIncome");
        BuildSlotUpgrades = P(BuildSlot, "Upgrades");
        BuildSlotInteractor = P(BuildSlot, "Interactor");
        CanBeHarvested = P(BuildingInteractor, "canBeHarvested") ?? P(BuildingInteractor, "CanBeHarvested");
        IsWaitingForChoice = P(BuildingInteractor, "IsWaitingForChoice");
        KnockedOutTonight = P(BuildingInteractor, "KnockedOutTonight");
        Balance = P(PlayerInteraction, "Balance");
        EnergyCoreBalance = P(PlayerInteraction, "EnergyCoreBalance");
        TrueBalance = P(PlayerInteraction, "TrueBalance");
        Transform = P(BuildSlot, "transform") ?? P(ComponentType, "transform");
        Position = P(TransformType, "position");
        GameObject = P(ComponentType, "gameObject");
        ChoiceCanBePicked = P(Choice, "CanBePicked");
        ChoiceCoroutineRunning = P(ChoiceManager, "ChoiceCoroutineRunning");

        BuildingName = F(BuildSlot, "buildingName");
        TargetBuilding = F(BuildingInteractor, "targetBuilding");
        BuildingInteractorOnSlot = F(BuildSlot, "buildingInteractor") ?? F(BuildSlot, "interactor");
        HarvestedToday = F(BuildingInteractor, "harvestedToday");
        WaitingForChoiceField = F(BuildingInteractor, "isWaitingForChoice");
        PlayerBuildingInteractors = F(TagManagerType, "playerBuildingInteractors");
        PlayerInteractionInstance = F(PlayerInteraction, "instance");
        PlayerMovementInstance = F(PlayerMovement, "instance");
        ChoiceManagerInstance = F(ChoiceManager, "instance");
        AvailableChoices = F(ChoiceManager, "availableChoices");
        ChoiceToReturn = F(ChoiceManager, "choiceToReturn");
        CurrentOriginBuildSlot = F(ChoiceManager, "currentOriginBuildSlot");
        ChoiceName = F(Choice, "name");
        ChoiceTooltip = F(Choice, "tooltip");
        UpgradeBranches = F(FindType("BuildSlot+Upgrade") ?? FindNested(BuildSlot, "Upgrade"), "upgradeBranches");
        ChoiceDetails = F(FindType("BuildSlot+UpgradeBranch") ?? FindNested(BuildSlot, "UpgradeBranch"), "choiceDetails");

        SlotsReady =
            BuildSlot != null &&
            BuildingInteractor != null &&
            PlayerInteraction != null &&
            TryToBuildOrUpgradeAndPay != null &&
            Harvest != null &&
            OnUpgradeChoiceComplete != null;

        if (!SlotsReady)
            Warn(logger, "slot commands will return unsupported_in_this_build until game types load");
    }

    public static object? GetStatic(FieldInfo? field) => field == null ? null : field.GetValue(null);

    public static object? GetStatic(PropertyInfo? prop) =>
        prop == null ? null : prop.GetValue(null, null);

    public static object? GetInstance() => GetStatic(CommandUnitsInstance);

    public static object? GetTagManager() => GetStatic(TagManagerInstance);

    public static object? GetDayNight() => GetStatic(DayNightCycleInstance);

    public static object? GetSceneTransition() => GetStatic(SceneTransitionManagerInstance);

    public static object? ToVector3(WorldVec v)
    {
        if (Vector3Type == null)
            return null;
        try
        {
            return Activator.CreateInstance(Vector3Type, v.X, v.Y, v.Z);
        }
        catch
        {
            var obj = Activator.CreateInstance(Vector3Type);
            SetMember(obj, "x", v.X);
            SetMember(obj, "y", v.Y);
            SetMember(obj, "z", v.Z);
            return obj;
        }
    }

    public static WorldVec FromVector3(object? v)
    {
        if (v == null)
            return default;
        return new WorldVec(GetFloat(v, "x"), GetFloat(v, "y"), GetFloat(v, "z"));
    }

    public static object? EnumValue(Type? enumType, int value)
    {
        if (enumType == null)
            return value;
        try
        {
            return Enum.ToObject(enumType, value);
        }
        catch
        {
            return value;
        }
    }

    public static object? NewETagList(params int[] tags)
    {
        if (ETagType == null)
            return null;
        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(ETagType);
        var list = Activator.CreateInstance(listType);
        var add = listType.GetMethod("Add");
        if (list == null || add == null)
            return null;
        foreach (var tag in tags)
            add.Invoke(list, new[] { EnumValue(ETagType, tag) });
        return list;
    }

    public static int GroupToETag(int group) =>
        group switch
        {
            1 => ETagGroup1,
            2 => ETagGroup2,
            3 => ETagGroup3,
            _ => -1
        };

    public static int ETagToGroup(int tag)
    {
        if (tag == ETagGroup1) return 1;
        if (tag == ETagGroup2) return 2;
        if (tag == ETagGroup3) return 3;
        return 0;
    }

    static FieldInfo? F(Type? t, string name) => t?.GetField(name, Any);
    static PropertyInfo? P(Type? t, string name) => t?.GetProperty(name, Any);

    static MethodInfo? M(Type? t, string name, int argc)
    {
        if (t == null)
            return null;
        foreach (var m in t.GetMethods(Any))
        {
            if (m.Name != name)
                continue;
            if (argc < 0 || m.GetParameters().Length == argc)
                return m;
        }

        return null;
    }

    static Type? FindNested(Type? parent, string name)
    {
        if (parent == null)
            return null;
        foreach (var n in parent.GetNestedTypes(Any))
        {
            if (n.Name == name)
                return n;
        }

        return null;
    }

    static int EnumInt(Type? enumType, string name, int fallback)
    {
        if (enumType == null)
            return fallback;
        try
        {
            foreach (var v in Enum.GetValues(enumType))
            {
                if (string.Equals(v.ToString(), name, StringComparison.Ordinal))
                    return Convert.ToInt32(v);
            }
        }
        catch
        {
            // keep fallback
        }

        return fallback;
    }

    static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? t = null;
            try
            {
                t = asm.GetType(fullName, false);
            }
            catch
            {
                // dynamic assemblies can throw
            }

            if (t == null && fullName.IndexOf('.') < 0 && fullName.IndexOf('+') < 0)
            {
                try
                {
                    t = asm.GetType("Thronefall." + fullName, false);
                }
                catch
                {
                    // dynamic assemblies can throw
                }
            }

            if (t != null)
                return t;
        }

        var simple = fullName;
        var plus = fullName.LastIndexOf('+');
        var dot = fullName.LastIndexOf('.');
        if (plus >= 0)
            simple = fullName.Substring(plus + 1);
        else if (dot >= 0)
            simple = fullName.Substring(dot + 1);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (var t in types)
            {
                if (t.Name != simple)
                    continue;
                if (string.Equals(t.FullName, fullName, StringComparison.Ordinal))
                    return t;
                if (plus < 0 && dot < 0 && string.IsNullOrEmpty(t.Namespace) && t.DeclaringType == null)
                    return t;
            }
        }

        return Type.GetType(fullName, false)
               ?? Type.GetType(fullName + ", Assembly-CSharp");
    }

    static float GetFloat(object obj, string name)
    {
        var v = GetMember(obj, name);
        return v == null ? 0f : Convert.ToSingle(v);
    }

    public static object? GetMember(object? obj, string name)
    {
        if (obj == null)
            return null;
        var t = obj.GetType();
        var f = t.GetField(name, Any);
        if (f != null)
            return f.GetValue(obj);
        var p = t.GetProperty(name, Any);
        return p?.GetValue(obj, null);
    }

    public static void SetMember(object? obj, string name, object? value)
    {
        if (obj == null)
            return;
        var t = obj.GetType();
        var f = t.GetField(name, Any);
        if (f != null)
        {
            f.SetValue(obj, value);
            return;
        }

        var p = t.GetProperty(name, Any);
        p?.GetSetMethod(true)?.Invoke(obj, new[] { value });
    }

    public static object? Call(object? obj, MethodInfo? method, params object?[] args)
    {
        if (method == null)
            return null;
        return method.Invoke(method.IsStatic ? null : obj, args);
    }

    public static object? CallNamed(object? obj, string name, params object?[] args)
    {
        if (obj == null)
            return null;
        var methods = obj.GetType().GetMethods(Any);
        foreach (var m in methods)
        {
            if (m.Name != name)
                continue;
            var ps = m.GetParameters();
            if (ps.Length != args.Length)
                continue;
            try
            {
                return m.Invoke(m.IsStatic ? null : obj, args);
            }
            catch
            {
                // try next overload
            }
        }

        return null;
    }

    static void Warn(object? logger, string message)
    {
        if (logger == null || string.IsNullOrEmpty(message))
            return;
        var t = logger.GetType();
        var m = t.GetMethod("LogWarning", new[] { typeof(object) })
                ?? t.GetMethod("LogWarning", new[] { typeof(string) });
        try
        {
            m?.Invoke(logger, new object[] { message });
        }
        catch
        {
            // logging must never throw
        }
    }
}
