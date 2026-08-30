using System;
using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public readonly struct WorldVec
{
    public WorldVec(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public float SqrMagnitude => X * X + Y * Y + Z * Z;
    public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

    public WorldVec Add(WorldVec o) => new(X + o.X, Y + o.Y, Z + o.Z);
    public WorldVec Sub(WorldVec o) => new(X - o.X, Y - o.Y, Z - o.Z);
    public WorldVec Scale(float s) => new(X * s, Y * s, Z * s);
    public float Dot(WorldVec o) => X * o.X + Y * o.Y + Z * o.Z;
    public float SqrDistance(WorldVec o) => Sub(o).SqrMagnitude;

    public WorldVec Normalized
    {
        get
        {
            var m = Magnitude;
            return m < 1e-6f ? default : Scale(1f / m);
        }
    }

    public Vec3Dto ToDto() => new() { X = X, Y = Y, Z = Z };

    public static WorldVec FromDto(Vec3Dto? dto) =>
        dto == null ? default : new WorldVec(dto.X, dto.Y, dto.Z);

    public static WorldVec operator +(WorldVec a, WorldVec b) => a.Add(b);
    public static WorldVec operator -(WorldVec a, WorldVec b) => a.Sub(b);
    public static WorldVec operator *(WorldVec a, float s) => a.Scale(s);
}

public readonly struct SpawnRally
{
    public SpawnRally(WorldVec point, bool pushedFromWall)
    {
        Point = point;
        PushedFromWall = pushedFromWall;
    }

    public WorldVec Point { get; }
    public bool PushedFromWall { get; }
}

public static class Spawns
{
    public static SpawnRally ComputeRally(
        IReadOnlyList<WorldVec> polyline,
        WorldVec? castle,
        float wallBackOffset,
        Func<WorldVec, float, bool>? isWallNear = null)
    {
        if (polyline == null || polyline.Count == 0)
            return new SpawnRally(castle ?? default, false);

        var onLine = castle is WorldVec c
            ? ClosestPointOnPolyline(polyline, c)
            : polyline[0];

        if (castle is not WorldVec castlePos)
            return new SpawnRally(onLine, false);

        var offset = Math.Max(0f, wallBackOffset);
        if (offset <= 0f || isWallNear == null || !isWallNear(onLine, offset + 1f))
            return new SpawnRally(onLine, false);

        var away = onLine.Sub(castlePos);
        if (away.SqrMagnitude < 1e-6f)
            away = polyline[polyline.Count - 1].Sub(polyline[0]);
        if (away.SqrMagnitude < 1e-6f)
            away = new WorldVec(0f, 0f, 1f);

        return new SpawnRally(onLine.Add(away.Normalized.Scale(offset)), true);
    }

    public static WorldVec ClosestPointOnPolyline(IReadOnlyList<WorldVec> polyline, WorldVec point)
    {
        if (polyline.Count == 1)
            return polyline[0];

        var best = polyline[0];
        var bestD = best.SqrDistance(point);
        for (var i = 0; i < polyline.Count - 1; i++)
        {
            var p = ClosestPointOnSegment(polyline[i], polyline[i + 1], point);
            var d = p.SqrDistance(point);
            if (d < bestD)
            {
                bestD = d;
                best = p;
            }
        }

        return best;
    }

    public static WorldVec ClosestPointOnSegment(WorldVec a, WorldVec b, WorldVec p)
    {
        var ab = b.Sub(a);
        var len2 = ab.SqrMagnitude;
        if (len2 < 1e-12f)
            return a;
        var t = p.Sub(a).Dot(ab) / len2;
        if (t <= 0f)
            return a;
        if (t >= 1f)
            return b;
        return a.Add(ab.Scale(t));
    }
}
