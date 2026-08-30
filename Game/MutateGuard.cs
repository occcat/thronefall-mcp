using System;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

/// <summary>
/// Shared transition + phase gate for mutate commands.
/// Callers pass the existing route message so ErrorCodes and wording stay put.
/// </summary>
public static class MutateGuard
{
    public static (int Status, string Code, string Message)? Check(
        bool transitionInProgress,
        string? phase,
        string transitionMessage,
        string illegalPhaseMessage,
        params string[] allowedPhases)
    {
        var blocked = Transition(transitionInProgress, transitionMessage);
        return blocked ?? Phase(phase, illegalPhaseMessage, allowedPhases);
    }

    public static (int Status, string Code, string Message)? Transition(
        bool transitionInProgress,
        string message)
    {
        if (transitionInProgress)
            return (409, ErrorCodes.TransitionInProgress, message);
        return null;
    }

    public static (int Status, string Code, string Message)? Phase(
        string? phase,
        string message,
        params string[] allowedPhases)
    {
        var current = (phase ?? "").Trim();
        if (allowedPhases != null)
        {
            foreach (var allowed in allowedPhases)
            {
                if (string.Equals(current, allowed, StringComparison.OrdinalIgnoreCase))
                    return null;
            }
        }

        return (409, ErrorCodes.IllegalPhase, message);
    }
}
