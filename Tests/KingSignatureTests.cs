using ThronefallControl.Dto;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Tests.GameFakes;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class KingSignatureTests : IDisposable
{
    public KingSignatureTests()
    {
        King.Reset();
        RuntimeState.Reset();
        PlayerMovement.instance = new PlayerMovement();
        DirectTagManager.instance = new DirectTagManager();
        ClosestTagManager.instance = new ClosestTagManager();
    }

    public void Dispose()
    {
        King.Reset();
        RuntimeState.Reset();
    }

    [Fact]
    public void Castle_lookup_uses_direct_etag_method()
    {
        var castle = new TaggedObject { transform = { position = new Vector3(8, 0, 2) } };
        DirectTagManager.instance.Castle.Add(castle);
        BindKing(typeof(DirectTagManager));
        King.Actions = King.ReflectionActions.Instance;

        var result = King.Teleport("castle", null);

        Assert.True(result.Ok);
        Assert.Equal(1, DirectTagManager.instance.DirectCalls);
        Assert.Equal(1, PlayerMovement.instance.TeleportCalls);
        Assert.Equal(8, PlayerMovement.instance.LastTeleport.x);
        Assert.Equal(2, PlayerMovement.instance.LastTeleport.z);
    }

    [Fact]
    public void Castle_lookup_binds_list_etag_args()
    {
        ClosestTagManager.instance.Castle.transform.position = new Vector3(3, 0, 5);
        BindKing(typeof(ClosestTagManager));
        King.Actions = King.ReflectionActions.Instance;

        var result = King.Teleport("castle", null);

        Assert.True(result.Ok);
        Assert.Equal(1, ClosestTagManager.instance.ClosestCalls);
        Assert.NotNull(ClosestTagManager.instance.LastMustHave);
        Assert.Contains(ETag.CastleCenter, ClosestTagManager.instance.LastMustHave!);
        Assert.NotNull(ClosestTagManager.instance.LastMayNotHave);
        Assert.Equal(3, PlayerMovement.instance.LastTeleport.x);
        Assert.Equal(5, PlayerMovement.instance.LastTeleport.z);
    }

    [Fact]
    public void Scripted_posts_is_intent_only()
    {
        var rec = new RecordingKingActions();
        var result = King.ApplyPolicy(NightPolicies.ScriptedPosts, actions: rec);
        Assert.True(result.Ok);
        Assert.True(result.Applied.IntentOnly);
        Assert.False(result.Applied.CommandUnits);
        Assert.False(result.Applied.TeleportKing);
        Assert.False(result.Applied.Invulnerable);
        Assert.Equal("intent_only", result.Applied.Combat);
        Assert.Equal(0, rec.CombatMutations);
        Assert.Contains("intent-only", result.Message);
    }

    static void BindKing(Type tagManagerType)
    {
        GameReflection.Types = name => name switch
        {
            "TagManager" => tagManagerType,
            "ETag" => typeof(ETag),
            "PlayerMovement" => typeof(PlayerMovement),
            "PlayerInteraction" => typeof(PlayerInteraction),
            _ => null
        };
    }
}