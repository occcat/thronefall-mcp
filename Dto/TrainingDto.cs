namespace ThronefallControl.Dto;

public sealed class TrainingDto
{
    public int SlotId { get; set; }
    public string BuildingName { get; set; } = "";
    public bool HasKnockedOut { get; set; }
    public float TimeTillNextRespawn { get; set; }
}
