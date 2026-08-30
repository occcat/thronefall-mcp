namespace ThronefallControl.Dto;

public sealed class EntityId
{
    public int InstanceId { get; set; }
    public int Generation { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
}
