using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public sealed class TrainingSnapshot
{
    public int SlotId { get; set; }
    public string BuildingName { get; set; } = "";
    public bool HasKnockedOut { get; set; }
    public float TimeTillNextRespawn { get; set; }
}

public static class Training
{
    public static TrainingDto FromSnapshot(TrainingSnapshot snapshot)
    {
        snapshot ??= new TrainingSnapshot();
        return new TrainingDto
        {
            SlotId = snapshot.SlotId,
            BuildingName = snapshot.BuildingName ?? "",
            HasKnockedOut = snapshot.HasKnockedOut,
            TimeTillNextRespawn = snapshot.TimeTillNextRespawn
        };
    }

    public static List<TrainingDto> ReadAll()
    {
        var result = new List<TrainingDto>();
        object[] spawners;
        try
        {
            spawners = UnityAccess.FindObjects("UnitRespawnerForBuildings");
        }
        catch
        {
            return result;
        }

        foreach (var spawner in spawners)
        {
            try
            {
                result.Add(FromSnapshot(ReadSnapshot(spawner)));
            }
            catch
            {
                // skip broken respawner
            }
        }

        return result;
    }

    static TrainingSnapshot ReadSnapshot(object spawner)
    {
        var slot = UnityAccess.Get(spawner, "myBuildSlot");
        var buildingName = UnityAccess.String(slot, "buildingName");
        if (string.IsNullOrEmpty(buildingName))
            buildingName = UnityAccess.NameOf(slot);
        return new TrainingSnapshot
        {
            SlotId = slot == null ? 0 : UnityAccess.InstanceId(slot),
            BuildingName = buildingName,
            HasKnockedOut = UnityAccess.Bool(spawner, "AtLeastOneUnitIsKnockedOut"),
            TimeTillNextRespawn = UnityAccess.Float(spawner, "timeTillNextRespawn")
        };
    }
}
