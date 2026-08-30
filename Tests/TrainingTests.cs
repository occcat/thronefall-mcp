using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class TrainingTests
{
    [Fact]
    public void FromSnapshot_maps_respawner_fields_to_dto()
    {
        var dto = Training.FromSnapshot(new TrainingSnapshot
        {
            SlotId = 4412,
            BuildingName = "Barracks",
            HasKnockedOut = true,
            TimeTillNextRespawn = 3.5f
        });

        Assert.Equal(4412, dto.SlotId);
        Assert.Equal("Barracks", dto.BuildingName);
        Assert.True(dto.HasKnockedOut);
        Assert.Equal(3.5f, dto.TimeTillNextRespawn);
    }

    [Fact]
    public void FromSnapshot_null_snapshot_yields_defaults()
    {
        var dto = Training.FromSnapshot(null!);
        Assert.Equal(0, dto.SlotId);
        Assert.Equal("", dto.BuildingName);
        Assert.False(dto.HasKnockedOut);
        Assert.Equal(0f, dto.TimeTillNextRespawn);
    }
}
