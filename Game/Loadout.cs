using System;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class Loadout
{
    public static LoadoutDto Snapshot()
    {
        try
        {
            return Observation.ReadLoadout() ?? new LoadoutDto();
        }
        catch
        {
            return new LoadoutDto();
        }
    }
}
