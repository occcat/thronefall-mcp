using System.Collections.Generic;

namespace ThronefallControl.Dto;

public sealed class HarvestRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public int? SlotId { get; set; }
    public int? Generation { get; set; }
    public bool TeleportKingNearby { get; set; }
}

public sealed class SlotMutateRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public int? Generation { get; set; }
    public bool TeleportKingNearby { get; set; }
    public string? Name { get; set; }
}

public sealed class HarvestResponse
{
    public bool Ok { get; set; } = true;
    public bool DryRun { get; set; }
    public int Harvested { get; set; }
    public int GoldGained { get; set; }
    public int EnergyCoreGained { get; set; }
    public int Balance { get; set; }
    public List<int> SlotIds { get; set; } = new();
}

public sealed class CancelChoiceResponse
{
    public bool Ok { get; set; } = true;
    public bool Canceled { get; set; }
    public string Phase { get; set; } = "";
    public int Generation { get; set; }
}

public sealed class SlotMutateResponse
{
    public bool Ok { get; set; } = true;
    public bool DryRun { get; set; }
    public string Action { get; set; } = "";
    public string? Slot { get; set; }
    public EntityId Id { get; set; } = new() { Kind = "slot" };
    public int Level { get; set; }
    public int Cost { get; set; }
    public int EnergyCoreCost { get; set; }
    public int Balance { get; set; }
    public bool NeedsChoice { get; set; }
    public bool Applied { get; set; }
    public List<ChoiceDto> Choices { get; set; } = new();
    public bool IsWaitingForChoice { get; set; }
}

public sealed class SlotCommandResult
{
    public bool Ok { get; set; } = true;
    public int Status { get; set; } = 200;
    public string? Error { get; set; }
    public string? Message { get; set; }
    public string Phase { get; set; } = "";
    public int Generation { get; set; }
    public bool DryRun { get; set; }
    public bool NeedsChoiceWait { get; set; }
    public object? Payload { get; set; }

    public static SlotCommandResult Fail(
        int status,
        string error,
        string message,
        string phase,
        int generation) =>
        new()
        {
            Ok = false,
            Status = status,
            Error = error,
            Message = message,
            Phase = phase,
            Generation = generation
        };

    public static SlotCommandResult Success(object payload, int generation, string phase, bool dryRun = false) =>
        new()
        {
            Ok = true,
            Status = 200,
            Payload = payload,
            Generation = generation,
            Phase = phase,
            DryRun = dryRun
        };
}
