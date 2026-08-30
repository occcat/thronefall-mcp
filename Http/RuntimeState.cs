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
}