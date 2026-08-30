using System;
using System.Reflection;
using ThronefallControl.Dto;

namespace ThronefallControl.Http;

public static class RuntimeState
{
    public static string Phase { get; set; } = Phases.Menu;
    public static int Generation { get; set; }
    public static bool Transitioning { get; set; }

    public static void Reset()
    {
        Phase = Phases.Menu;
        Generation = 0;
        Transitioning = false;
    }

    public static void RefreshFromGame()
    {
        try
        {
            var stmType = Type.GetType("SceneTransitionManager, Assembly-CSharp");
            if (stmType == null)
                return;
            var stm = stmType.GetField("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (stm == null)
                return;

            var running = ReadBool(stm, "SceneTransitionIsRunning");
            Transitioning = running;
            if (running)
            {
                Phase = Phases.Transition;
                return;
            }

            var stateName = ReadMember(stm, "CurrentSceneState")?.ToString() ?? "";
            if (Contains(stateName, "LevelSelect"))
            {
                Phase = Phases.LevelSelect;
                return;
            }

            if (Contains(stateName, "MainMenu"))
            {
                Phase = Phases.Menu;
                return;
            }

            var timestate = ReadTimestate();
            if (Contains(timestate, "Night"))
            {
                Phase = Phases.Night;
                return;
            }

            if (Contains(timestate, "Day") || Contains(stateName, "InGame"))
            {
                Phase = Phases.Day;
                return;
            }
        }
        catch
        {
            // Keep the last known phase; never throw out of Update.
        }
    }

    static string ReadTimestate()
    {
        var dncType = Type.GetType("DayNightCycle, Assembly-CSharp");
        if (dncType == null)
            return "";
        var dnc = dncType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (dnc == null)
            return "";
        return ReadMember(dnc, "CurrentTimestate")?.ToString() ?? "";
    }

    static bool ReadBool(object target, string name)
    {
        var value = ReadMember(target, name);
        return value is bool b && b;
    }

    static object? ReadMember(object target, string name)
    {
        var type = target.GetType();
        return type.GetProperty(name)?.GetValue(target)
               ?? type.GetField(name)?.GetValue(target);
    }

    static bool Contains(string value, string token) =>
        value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
}