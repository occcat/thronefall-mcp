using System;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class DayNight
{
    public static ClockDto Snapshot()
    {
        try
        {
            return Observation.ReadClock() ?? new ClockDto();
        }
        catch
        {
            return new ClockDto();
        }
    }

    public static EconomyDto SnapshotEconomy()
    {
        try
        {
            return Observation.ReadEconomy() ?? new EconomyDto();
        }
        catch
        {
            return new EconomyDto();
        }
    }
}
