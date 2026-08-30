using System;
using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class Paths
{
    public static List<CutterDto> Snapshot(IdRegistry ids)
    {
        try
        {
            return Observation.ReadCutters(ids) ?? new List<CutterDto>();
        }
        catch
        {
            return new List<CutterDto>();
        }
    }
}
