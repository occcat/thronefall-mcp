using System;
using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class Spawns
{
    public static List<SpawnLineDto> Snapshot(IdRegistry ids)
    {
        try
        {
            return Observation.ReadSpawns(ids) ?? new List<SpawnLineDto>();
        }
        catch
        {
            return new List<SpawnLineDto>();
        }
    }
}
