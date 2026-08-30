using System;
using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class Units
{
    public static List<UnitDto> Snapshot(IdRegistry ids)
    {
        try
        {
            return Observation.ReadUnits(ids) ?? new List<UnitDto>();
        }
        catch
        {
            return new List<UnitDto>();
        }
    }

    public static EnemySummaryDto SnapshotEnemies(IdRegistry ids)
    {
        try
        {
            return Observation.ReadEnemies(ids) ?? new EnemySummaryDto();
        }
        catch
        {
            return new EnemySummaryDto();
        }
    }
}
