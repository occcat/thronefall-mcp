using System;
using System.Collections.Generic;

namespace ThronefallControl.Tests.GameFakes;

public class Equippable
{
    public string displayName = "";
    public string description = "";
    public bool IsUnlocked { get; set; } = true;
    public string GetLockedTooltip() => IsUnlocked ? "" : "Locked";
}

public class TFUIEquippable
{
    public Equippable equippableData = new();
    public Equippable Data => equippableData;
    public bool locked;
    public bool Locked => locked;
    public bool isPerk;
    public bool isWeapon;
    public bool isMutator;
    public int PickCalls;
    public void Pick() => PickCalls++;
}

public class LoadoutUIHelper
{
    public static int TrySelectCalls;
    public List<TFUIEquippable> perks = new();
    public List<TFUIEquippable> weapons = new();
    public List<TFUIEquippable> mutators = new();

    public void TrySelectEquippableForLoadout() => TrySelectCalls++;
}

public class PerkSelectionItem
{
    public Equippable equippable = new();
    public Equippable Equippable => equippable;
    public PerkSelectionGroup? perkSelectionGroup;
}

public class PerkSelectionGroup
{
    public static int StringSelects;
    public static int ItemSelects;

    public void SelectPerk(string name)
    {
        _ = name;
        StringSelects++;
    }

    public void SelectPerk(PerkSelectionItem item)
    {
        _ = item;
        ItemSelects++;
    }
}

public class PlayerInteraction
{
    public static PlayerInteraction instance = new();
}

public class LevelData
{
}

public class Quest
{
    public string questType = "";
    public string statement = "";
    public bool beaten;

    public string GetMissionStatement() =>
        string.IsNullOrEmpty(statement) ? questType : statement;

    public bool CheckBeaten(object? data)
    {
        _ = data;
        return beaten;
    }
}

public class LevelInfo
{
    public string sceneName = "";
    public List<Quest> quests = new();
    public LevelData LevelData { get; } = new();
}

public class LevelInteractor
{
    public static int ZeroArgBegins;
    public static int PlayerBegins;
    public LevelInfo levelInfo = new();
    public PlayerInteraction? LastPlayer;
    public bool CanBePlayed { get; set; } = true;

    public void InteractionBegin() => ZeroArgBegins++;

    public void InteractionBegin(PlayerInteraction player)
    {
        PlayerBegins++;
        LastPlayer = player;
    }
}

public class LevelSelectManager
{
    public static LevelSelectManager instance = new();
    public List<LevelInteractor> levelInteractors = new();
    public int PlayPressed;
    public static int LoadSceneCalls;

    public void PlayButtonPressed() => PlayPressed++;

    public void LoadScene(string scene)
    {
        _ = scene;
        LoadSceneCalls++;
    }
}

public struct Vector3
{
    public float x;
    public float y;
    public float z;

    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

public class Transform
{
    public Vector3 position;
}

public class TaggedObject
{
    public Transform transform = new();
}

public enum ETag
{
    NONE = 0,
    PlayerOwned = 1,
    EnemyOwned = 2,
    Player = 3,
    CastleCenter = 4
}

public class PlayerMovement
{
    public static PlayerMovement instance = new();
    public Transform transform = new();
    public Vector3 LastTeleport;
    public int TeleportCalls;

    public void TeleportTo(Vector3 p)
    {
        TeleportCalls++;
        LastTeleport = p;
    }

    public void TeleportToStart() { }
}

public class DirectTagManager
{
    public static DirectTagManager instance = new();
    public int DirectCalls;
    public List<TaggedObject> Castle = new();

    public List<TaggedObject> FindAllTaggedObjectsWithTagDirect_UseWithCare(ETag tag)
    {
        DirectCalls++;
        return tag == ETag.CastleCenter ? Castle : new List<TaggedObject>();
    }
}

public class ClosestTagManager
{
    public static ClosestTagManager instance = new();
    public int ClosestCalls;
    public List<ETag>? LastMustHave;
    public List<ETag>? LastMayNotHave;
    public TaggedObject Castle = new();

    public TaggedObject FindClosestTaggedObjectWithTags(Vector3 origin, List<ETag> mustHave, List<ETag> mayNotHave)
    {
        _ = origin;
        if (mustHave == null)
            throw new ArgumentNullException(nameof(mustHave));
        if (mayNotHave == null)
            throw new ArgumentNullException(nameof(mayNotHave));
        ClosestCalls++;
        LastMustHave = mustHave;
        LastMayNotHave = mayNotHave;
        return Castle;
    }
}