using ThronefallControl.Dto;
using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

[Collection(GameFacadeCollection.Name)]
public sealed class UnityAccessCacheTests : IDisposable
{
    public UnityAccessCacheTests()
    {
        UnityAccess.TraceLookups = false;
        UnityAccess.ResetLookupTrace();
        UnityAccessCacheSingletonProbe.Reads = 0;
    }

    public void Dispose()
    {
        UnityAccess.TraceLookups = false;
        UnityAccess.ResetLookupTrace();
        UnityAccessCacheSingletonProbe.Reads = 0;
    }

    [Fact]
    public void Repeated_Get_on_same_type_matches_and_skips_GetProperty()
    {
        var slots = new HotSlot[70];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new HotSlot
            {
                buildingName = "House" + i,
                Level = i,
                GoldIncome = i * 2,
                CanBeUpgraded = i % 2 == 0
            };
        }

        UnityAccess.TraceLookups = true;
        UnityAccess.ResetLookupTrace();
        AssertSlots(slots);
        var first = UnityAccess.PropertyLookups;
        Assert.True(first > 0);

        UnityAccess.ResetLookupTrace();
        AssertSlots(slots);
        Assert.Equal(0, UnityAccess.PropertyLookups);
        Assert.True(first < slots.Length, "first pass should hit unique members, not every slot");
    }

    [Fact]
    public void Get_keeps_DeclaredOnly_base_walk_and_hiding()
    {
        var derived = new DerivedSlot { buildingName = "Mill", GoldIncome = 5 };
        Assert.Equal("Mill", UnityAccess.String(derived, "buildingName"));
        Assert.Equal(5, UnityAccess.Int(derived, "GoldIncome"));
        Assert.Equal(1, UnityAccess.Int(new BaseSlot(), "GoldIncome"));
        Assert.Null(UnityAccess.Get(derived, "missingMember"));
    }

    [Fact]
    public void Call_caches_MethodInfo_not_result()
    {
        var counter = new CallProbe();
        Assert.Equal(1, UnityAccess.Call(counter, "Next"));
        Assert.Equal(2, UnityAccess.Call(counter, "Next"));
        Assert.Equal(3, UnityAccess.Call(counter, "Add", 1, 2));
        Assert.Equal("base", UnityAccess.Call(new DerivedCaller(), "Name"));
        Assert.Equal(2, UnityAccess.Call(new DerivedCaller(), "Level"));

        UnityAccess.TraceLookups = true;
        UnityAccess.ResetLookupTrace();
        _ = UnityAccess.Call(counter, "Next");
        Assert.Equal(0, UnityAccess.MethodLookups);
    }

    [Fact]
    public void GetStatic_reads_live_value()
    {
        var typeName = typeof(UnityAccessCacheStaticProbe).FullName!;
        UnityAccessCacheStaticProbe.CurrentSave = 3;
        Assert.Equal(3, Convert.ToInt32(UnityAccess.GetStatic(typeName, "CurrentSave")));
        UnityAccessCacheStaticProbe.CurrentSave = 9;
        Assert.Equal(9, Convert.ToInt32(UnityAccess.GetStatic(typeName, "CurrentSave")));
    }

    [Fact]
    public void Singleton_and_FindObjects_are_reused_in_one_scope_only()
    {
        var typeName = typeof(UnityAccessCacheSingletonProbe).FullName!;
        UnityAccessCacheSingletonProbe.Reads = 0;
        using (UnityAccess.BeginRequestScope())
        {
            var a = UnityAccess.Singleton(typeName);
            var b = UnityAccess.Singleton(typeName);
            Assert.Same(a, b);
            Assert.Equal(1, UnityAccessCacheSingletonProbe.Reads);
            Assert.Equal(12, UnityAccess.Int(a, "Balance"));

            UnityAccess.TraceLookups = true;
            UnityAccess.ResetLookupTrace();
            UnityAccess.FindObjects("BuildSlot");
            UnityAccess.FindObjects("BuildSlot");
            UnityAccess.FindObjects("EnemySpawnLine");
            Assert.Equal(2, UnityAccess.FindObjectsLookups);
        }

        var again = UnityAccess.Singleton(typeName);
        Assert.Equal(2, UnityAccessCacheSingletonProbe.Reads);
        Assert.Equal(12, UnityAccess.Int(again, "Balance"));

        UnityAccess.ResetLookupTrace();
        UnityAccess.FindObjects("BuildSlot");
        Assert.Equal(1, UnityAccess.FindObjectsLookups);
    }

    [Fact]
    public void Capture_twice_drops_GetProperty_and_does_not_keep_FindObjects()
    {
        var facade = new GameFacade(new LiveWorld());
        var include = StateInclude.Parse(null);

        UnityAccess.TraceLookups = true;
        UnityAccess.ResetLookupTrace();
        var first = new StateDto();
        Observation.Capture(facade, first, include);
        var firstProps = UnityAccess.PropertyLookups;
        var firstFinds = UnityAccess.FindObjectsLookups;

        UnityAccess.ResetLookupTrace();
        var second = new StateDto();
        Observation.Capture(facade, second, include);
        Assert.True(UnityAccess.PropertyLookups < firstProps || firstProps == 0);
        Assert.Equal(0, UnityAccess.PropertyLookups);
        Assert.Equal(firstFinds, UnityAccess.FindObjectsLookups);
        Assert.Equal(first.Economy.Balance, second.Economy.Balance);
        Assert.Equal(first.Slots?.Count ?? 0, second.Slots?.Count ?? 0);
        Assert.Equal(first.Loadout?.AsString.Count ?? 0, second.Loadout?.AsString.Count ?? 0);
    }

    [Fact]
    public void LoadoutCatalog_Get_matches_after_member_cache()
    {
        var locked = new CatalogUi
        {
            equippableData = { displayName = "God King", description = "meta perk", IsUnlocked = false },
            locked = true,
            isPerk = true
        };
        var first = LoadoutCatalog.MapUiItem(locked, "weapon");
        var second = LoadoutCatalog.MapUiItem(locked, "weapon");
        Assert.NotNull(first);
        Assert.Equal(first!.Name, second!.Name);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.Locked, second.Locked);
        Assert.Equal("God King", first.Name);
        Assert.Equal("perk", first.Kind);
        Assert.True(first.Locked);
    }

    static void AssertSlots(HotSlot[] slots)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            Assert.Equal("House" + i, UnityAccess.String(slot, "buildingName"));
            Assert.Equal(i, UnityAccess.Int(slot, "Level"));
            Assert.Equal(i * 2, UnityAccess.Int(slot, "GoldIncome"));
            Assert.Equal(i % 2 == 0, UnityAccess.Bool(slot, "CanBeUpgraded"));
        }
    }

    sealed class HotSlot
    {
        public string buildingName = "";
        public int Level { get; set; }
        public int GoldIncome { get; set; }
        public bool CanBeUpgraded { get; set; }
    }

    class BaseSlot
    {
        public int GoldIncome { get; set; } = 1;
    }

    sealed class DerivedSlot : BaseSlot
    {
        public string buildingName = "";
        public new int GoldIncome { get; set; }
    }

    sealed class CallProbe
    {
        public int N;
        public int Next() => ++N;
        public int Add(int a, int b) => a + b;
    }

    class BaseCaller
    {
        public string Name() => "base";
        public int Level() => 1;
    }

    sealed class DerivedCaller : BaseCaller
    {
        public new int Level() => 2;
    }

    sealed class CatalogUi
    {
        public CatalogData equippableData = new();
        public CatalogData Data => equippableData;
        public bool locked;
        public bool Locked => locked;
        public bool isPerk;
    }

    sealed class CatalogData
    {
        public string displayName = "";
        public string description = "";
        public bool IsUnlocked { get; set; } = true;
    }
}

public class UnityAccessCacheStaticProbe
{
    public static int CurrentSave;
}

public class UnityAccessCacheSingletonProbe
{
    public static int Reads;
    static readonly UnityAccessCacheSingletonProbe InstanceValue = new();

    public static UnityAccessCacheSingletonProbe instance
    {
        get
        {
            Reads++;
            return InstanceValue;
        }
    }

    public int Balance => 12;
}
