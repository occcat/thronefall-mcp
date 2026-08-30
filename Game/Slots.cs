using System;
using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class Slots
{
    public static List<SlotDto> Snapshot(IdRegistry ids)
    {
        try
        {
            return Observation.ReadSlots(ids) ?? new List<SlotDto>();
        }
        catch
        {
            return new List<SlotDto>();
        }
    }
}
