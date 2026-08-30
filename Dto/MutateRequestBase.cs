namespace ThronefallControl.Dto;

public abstract class MutateRequestBase
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
}
