using System.Collections.Generic;

namespace ThronefallControl.Dto;

public sealed class UnitSelectorDto
{
    public List<int>? Ids { get; set; }
    public string? TypeName { get; set; }
    public int? Group { get; set; }
}

public sealed class UnitsCommandRequest : MutateRequestBase
{
    public UnitSelectorDto? Selector { get; set; }
    public Vec3Dto? Target { get; set; }
    public bool Hold { get; set; } = true;
    public bool? UseSolver { get; set; }
}

public sealed class UnitsHoldRequest : MutateRequestBase
{
    public UnitSelectorDto? Selector { get; set; }
}

public sealed class UnitsFollowRequest : MutateRequestBase
{
    public UnitSelectorDto? Selector { get; set; }
}

public sealed class UnitsGroupRequest : MutateRequestBase
{
    public UnitSelectorDto? Selector { get; set; }
    public int Group { get; set; }
    public bool Clear { get; set; }
}

public sealed class UnitPickDto
{
    public List<int>? Ids { get; set; }
    public string? TypeName { get; set; }
    public int Count { get; set; }
}

public sealed class UnitsDeployRequest : MutateRequestBase
{
    public List<UnitPickDto>? Picks { get; set; }
    public Vec3Dto? Target { get; set; }
    public bool Hold { get; set; } = true;
    public float Spacing { get; set; } = 2f;
}

public sealed class UnitsSendToSpawnRequest : MutateRequestBase
{
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
