namespace ThronefallControl.Dto;

public sealed class ErrorResponse
{
    public bool Ok { get; set; }
    public string Error { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Phase { get; set; }
    public int? Generation { get; set; }
}
