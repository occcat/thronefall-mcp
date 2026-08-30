using System;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class King
{
    public static KingDto Snapshot(IdRegistry ids)
    {
        try
        {
            return Observation.ReadKing(ids) ?? new KingDto();
        }
        catch
        {
            return new KingDto();
        }
    }
}
