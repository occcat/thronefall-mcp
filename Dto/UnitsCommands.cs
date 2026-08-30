using System.Collections.Generic;

namespace ThronefallControl.Dto;

public sealed class UnitSelectorDto
{
    public List<int>? Ids { get; set; }
    public string? TypeName { get; set; }
    public int? Group { get; set; }
}

public sealed class UnitsCommandRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public UnitSelectorDto? Selector { get; set; }
    public Vec3Dto? Target { get; set; }
    public bool Hold { get; set; } = true;
    public bool? UseSolver { get; set; }
}

public sealed class UnitsHoldRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public UnitSelectorDto? Selector { get; set; }
}

public sealed class UnitsFollowRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public UnitSelectorDto? Selector { get; set; }
}

public sealed class UnitsGroupRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public UnitSelectorDto? Selector { get; set; }
    public int Group { get; set; }
    public bool Clear { get; set; }
}

public sealed class UnitsSendToSpawnRequest
{
    public string? ClientRequestId { get; set; }
    public bool DryRun { get; set; }
    public UnitSelectorDto? Selector { get; set; }
    public string? TypeName { get; set; }
    public int SpawnId { get; set; }
    public EntityId? Spawn { get; set; }
    public bool Hold { get; set; } = true;
    public bool? UseSolver { get; set; }
}

public sealed class UnitsCommandResponse
{
    public bool Ok { get; set; } = true;
    public bool DryRun { get; set; }
    public string Path { get; set; } = "fallback";
    public List<int> Applied { get; set; } = new();
    public List<int> StaleIds { get; set; } = new();
    public List<int> NotFound { get; set; } = new();
    public Vec3Dto? Target { get; set; }
    public int Group { get; set; }
    public DryRunWouldDto? Would { get; set; }
}
