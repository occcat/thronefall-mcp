using System;
using System.Reflection;

namespace ThronefallControl.Game;

public static class ReflectionCache
{
    public static bool SlotsReady { get; private set; }

    public static Type? BuildSlot { get; private set; }
    public static Type? BuildingInteractor { get; private set; }
    public static Type? PlayerInteraction { get; private set; }
    public static Type? PlayerMovement { get; private set; }
    public static Type? ChoiceManager { get; private set; }
    public static Type? Choice { get; private set; }
    public static Type? TagManager { get; private set; }
    public static Type? DayNightCycle { get; private set; }
    public static Type? SceneTransitionManager { get; private set; }
    public static Type? SceneManager { get; private set; }
    public static Type? UnityObject { get; private set; }

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
    public static PropertyInfo? DayNightInstance { get; private set; }
    public static PropertyInfo? CurrentTimestate { get; private set; }
    public static PropertyInfo? SceneTransitionIsRunning { get; private set; }
    public static PropertyInfo? CurrentSceneState { get; private set; }
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
    public static FieldInfo? TagManagerInstance { get; private set; }
    public static FieldInfo? ChoiceManagerInstance { get; private set; }
    public static FieldInfo? SceneTransitionInstance { get; private set; }
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
            BuildSlot = FindType("BuildSlot");
            BuildingInteractor = FindType("BuildingInteractor");
            PlayerInteraction = FindType("PlayerInteraction");
            PlayerMovement = FindType("PlayerMovement");
            ChoiceManager = FindType("ChoiceManager");
            Choice = FindType("Choice");
            TagManager = FindType("TagManager");
            DayNightCycle = FindType("DayNightCycle");
            SceneTransitionManager = FindType("SceneTransitionManager");
            SceneManager = FindType("UnityEngine.SceneManagement.SceneManager");
            UnityObject = FindType("UnityEngine.Object");

            TryToBuildOrUpgradeAndPay = Method(BuildSlot, "TryToBuildOrUpgradeAndPay");
            ExecuteBuildOrUpgrade = Method(BuildSlot, "ExecuteBuildOrUpgrade");
            OnUpgradeChoiceComplete = Method(BuildSlot, "OnUpgradeChoiceComplete");
            Harvest = Method(BuildingInteractor, "Harvest");
            MarkAsHarvested = Method(BuildingInteractor, "MarkAsHarvested");
            SpendCoins = Method(PlayerInteraction, "SpendCoins");
            SpendEnergyCores = Method(PlayerInteraction, "SpendEnergyCores");
            TeleportTo = Method(PlayerMovement, "TeleportTo");
            GetInstanceId = Method(UnityObject, "GetInstanceID") ?? Method(FindType("UnityEngine.GameObject"), "GetInstanceID");
            GetActiveScene = Method(SceneManager, "GetActiveScene", BindingFlags.Public | BindingFlags.Static);

            BuildSlotLevel = Prop(BuildSlot, "Level");
            CanBeUpgraded = Prop(BuildSlot, "CanBeUpgraded");
            NextUpgradeIsChoice = Prop(BuildSlot, "NextUpgradeIsChoice");
            NextUpgradeOrBuildCost = Prop(BuildSlot, "NextUpgradeOrBuildCost");
            NextUpgradeOrBuildEnergyCoreCost = Prop(BuildSlot, "NextUpgradeOrBuildEnergyCoreCost");
            GoldIncome = Prop(BuildSlot, "GoldIncome");
            EnergyCoreIncome = Prop(BuildSlot, "EnergyCoreIncome");
            BuildSlotUpgrades = Prop(BuildSlot, "Upgrades");
            BuildSlotInteractor = Prop(BuildSlot, "Interactor");
            CanBeHarvested = Prop(BuildingInteractor, "canBeHarvested");
            IsWaitingForChoice = Prop(BuildingInteractor, "IsWaitingForChoice");
            KnockedOutTonight = Prop(BuildingInteractor, "KnockedOutTonight");
            Balance = Prop(PlayerInteraction, "Balance");
            EnergyCoreBalance = Prop(PlayerInteraction, "EnergyCoreBalance");
            TrueBalance = Prop(PlayerInteraction, "TrueBalance");
            DayNightInstance = Prop(DayNightCycle, "Instance", BindingFlags.Public | BindingFlags.Static);
            CurrentTimestate = Prop(DayNightCycle, "CurrentTimestate");
            SceneTransitionIsRunning = Prop(SceneTransitionManager, "SceneTransitionIsRunning");
            CurrentSceneState = Prop(SceneTransitionManager, "CurrentSceneState");
            Transform = Prop(BuildSlot, "transform") ?? Prop(FindType("UnityEngine.Component"), "transform");
            Position = Prop(FindType("UnityEngine.Transform"), "position");
            GameObject = Prop(FindType("UnityEngine.Component"), "gameObject");
            ChoiceCanBePicked = Prop(Choice, "CanBePicked");
            ChoiceCoroutineRunning = Prop(ChoiceManager, "ChoiceCoroutineRunning");

            BuildingName = Field(BuildSlot, "buildingName");
            TargetBuilding = Field(BuildingInteractor, "targetBuilding");
            BuildingInteractorOnSlot = Field(BuildSlot, "buildingInteractor") ?? Field(BuildSlot, "interactor");
            HarvestedToday = Field(BuildingInteractor, "harvestedToday");
            WaitingForChoiceField = Field(BuildingInteractor, "isWaitingForChoice");
            PlayerBuildingInteractors = Field(TagManager, "playerBuildingInteractors");
            PlayerInteractionInstance = Field(PlayerInteraction, "instance", BindingFlags.Public | BindingFlags.Static);
            PlayerMovementInstance = Field(PlayerMovement, "instance", BindingFlags.Public | BindingFlags.Static);
            TagManagerInstance = Field(TagManager, "instance", BindingFlags.Public | BindingFlags.Static);
            ChoiceManagerInstance = Field(ChoiceManager, "instance", BindingFlags.Public | BindingFlags.Static);
            SceneTransitionInstance = Field(SceneTransitionManager, "instance", BindingFlags.Public | BindingFlags.Static);
            AvailableChoices = Field(ChoiceManager, "availableChoices");
            ChoiceToReturn = Field(ChoiceManager, "choiceToReturn");
            CurrentOriginBuildSlot = Field(ChoiceManager, "currentOriginBuildSlot");
            ChoiceName = Field(Choice, "name");
            ChoiceTooltip = Field(Choice, "tooltip");
            UpgradeBranches = Field(FindType("BuildSlot+Upgrade") ?? FindNested(BuildSlot, "Upgrade"), "upgradeBranches");
            ChoiceDetails = Field(FindType("BuildSlot+UpgradeBranch") ?? FindNested(BuildSlot, "UpgradeBranch"), "choiceDetails");

            SlotsReady =
                BuildSlot != null &&
                BuildingInteractor != null &&
                PlayerInteraction != null &&
                TryToBuildOrUpgradeAndPay != null &&
                Harvest != null &&
                OnUpgradeChoiceComplete != null;
        }
        catch (Exception ex)
        {
            SlotsReady = false;
            Log(logger, "slot reflection init failed: " + ex.Message);
        }

        if (!SlotsReady)
            Log(logger, "slot commands will return unsupported_in_this_build until game types load");
    }

    public static Type? FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType(name);
            if (type != null)
                return type;
            if (name.IndexOf('.') < 0)
            {
                type = asm.GetType(name) ?? asm.GetType("Thronefall." + name);
                if (type != null)
                    return type;
            }
        }

        return Type.GetType(name) ?? Type.GetType(name + ", Assembly-CSharp");
    }

    static Type? FindNested(Type? parent, string name)
    {
        if (parent == null)
            return null;
        foreach (var nested in parent.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (nested.Name == name)
                return nested;
        }

        return null;
    }

    static MethodInfo? Method(Type? type, string name, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) =>
        type?.GetMethod(name, flags);

    static PropertyInfo? Prop(Type? type, string name, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) =>
        type?.GetProperty(name, flags);

    static FieldInfo? Field(Type? type, string name, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) =>
        type?.GetField(name, flags);

    static void Log(object? logger, string message)
    {
        if (logger == null)
            return;
        var method = logger.GetType().GetMethod("LogWarning", new[] { typeof(object) })
            ?? logger.GetType().GetMethod("LogWarning", new[] { typeof(string) });
        method?.Invoke(logger, new object[] { message });
    }
}
