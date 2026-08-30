using ThronefallControl.Dto;
using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class IdRegistryTests
{
    [Fact]
    public void Register_resolves_with_matching_generation()
    {
        var ids = new IdRegistry();
        ids.BeginScene();
        var marker = new object();
        var id = ids.Register(42, "slot", "House", marker);
        Assert.Equal(ids.SceneGeneration, id.Generation);
        Assert.Equal("slot", id.Kind);
        Assert.Equal("House", id.Name);
        Assert.True(ids.TryResolve(id.InstanceId, id.Generation, out var found, out var error));
        Assert.Same(marker, found);
        Assert.Null(error);
    }

    [Fact]
    public void Scene_change_makes_old_ids_stale()
    {
        var ids = new IdRegistry();
        ids.BeginScene();
        var id = ids.Register(42, "slot", "House", new object());
        var oldGen = id.Generation;
        ids.BeginScene();
        Assert.NotEqual(oldGen, ids.SceneGeneration);
        Assert.False(ids.TryResolve(id.InstanceId, oldGen, out _, out var error));
        Assert.Equal(ErrorCodes.StaleId, error);
    }

    [Fact]
    public void Unknown_id_is_not_found()
    {
        var ids = new IdRegistry();
        ids.BeginScene();
        Assert.False(ids.TryResolve(99, ids.SceneGeneration, out _, out var error));
        Assert.Equal(ErrorCodes.NotFound, error);
    }

    [Fact]
    public void Matching_generation_without_entry_is_not_found()
    {
        var ids = new IdRegistry();
        ids.BeginScene();
        ids.Register(1, "unit", "P Knight", new object());
        ids.BeginScene();
        Assert.False(ids.TryResolve(1, ids.SceneGeneration, out _, out var error));
        Assert.Equal(ErrorCodes.NotFound, error);
    }
}
