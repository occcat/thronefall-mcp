namespace ThronefallControl.Dto;

public sealed class HealthResponse
{
    public bool Ok { get; set; } = true;
    public string Plugin { get; set; } = "ThronefallControl";
    public string Version { get; set; } = "0.1.0";
    public string GameVersion { get; set; } = "2.13";
    public string Bound { get; set; } = "";
    public string? Phase { get; set; }
    public int Generation { get; set; }
    public string? Scene { get; set; }
    public bool CheatsEnabled { get; set; }
    public double UptimeSeconds { get; set; }
}
