using System;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public sealed class CallNightResult
{
    public int Status { get; set; } = 200;
    public string? Error { get; set; }
    public string Message { get; set; } = "";
    public string Phase { get; set; } = "";
    public int Generation { get; set; }
    public bool DryRun { get; set; }
    public bool Called { get; set; }
    public DryRunWouldDto? Would { get; set; }
}

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

    public static CallNightResult CallNight(bool dryRun) =>
        CallNight(GameFacade.Current, dryRun);

    public static CallNightResult CallNight(GameFacade game, bool dryRun)
    {
        var world = game.World;
        var generation = game.Ids.SceneGeneration;
        var phase = world.Phase ?? "";

        var blocked = MutateGuard.Check(
            world.SceneTransitionIsRunning
                || string.Equals(phase, Phases.Transition, StringComparison.OrdinalIgnoreCase),
            phase,
            "scene transition in progress",
            $"POST /night/call is illegal in phase={phase}",
            Phases.Day);
        if (blocked is { } gate)
        {
            return Fail(
                gate.Status,
                gate.Code,
                gate.Message,
                gate.Code == ErrorCodes.TransitionInProgress ? Phases.Transition : phase,
                generation);
        }

        if (!world.SwitchToNightSupported)
        {
            return Fail(
                501,
                ErrorCodes.UnsupportedInThisBuild,
                "DayNightCycle.SwitchToNight is unavailable",
                phase,
                generation);
        }

        if (dryRun)
        {
            return new CallNightResult
            {
                Status = 200,
                Phase = phase,
                Generation = generation,
                DryRun = true,
                Would = new DryRunWouldDto
                {
                    Action = "call_night",
                    Cost = 0,
                    BalanceAfter = world.Balance,
                    Blocked = false
                }
            };
        }

        world.SwitchToNight();
        return new CallNightResult
        {
            Status = 200,
            Phase = world.Phase ?? "",
            Generation = generation,
            Called = true
        };
    }

    static CallNightResult Fail(int status, string error, string message, string phase, int generation) =>
        new()
        {
            Status = status,
            Error = error,
            Message = message,
            Phase = phase,
            Generation = generation
        };
}
