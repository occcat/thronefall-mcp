namespace ThronefallControl.Dto;

public sealed class LoadoutSelectRequest : MutateRequestBase
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
}

public sealed class LoadoutSelectResult
{
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }
    public string? Message { get; set; }
    public bool DryRun { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool Selected { get; set; }
    public bool WouldSelect { get; set; }
}

public sealed class LevelStartRequest : MutateRequestBase
{
    public string SceneName { get; set; } = "";
}

public sealed class LevelStartResult
{
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }
    public string? Message { get; set; }
    public bool DryRun { get; set; }
    public string SceneName { get; set; } = "";
    public bool Started { get; set; }
    public bool WouldStart { get; set; }
}

public sealed class KingTeleportRequest : MutateRequestBase
{
    public string? Target { get; set; }
    public Vec3Dto? Position { get; set; }
}

public sealed class KingTeleportResult
{
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }
    public string? Message { get; set; }
    public bool DryRun { get; set; }
    public string Target { get; set; } = "";
    public Vec3Dto Position { get; set; } = new();
    public bool Invulnerable { get; set; }
    public bool Teleported { get; set; }
}

public sealed class NightPolicyRequest : MutateRequestBase
{
    public string Policy { get; set; } = NightPolicies.Human;
}

public sealed class NightPolicyAppliedDto
{
    public bool TeleportKing { get; set; }
    public bool ChangeHold { get; set; }
    public bool CommandUnits { get; set; }
    public bool Invulnerable { get; set; }
    public bool IntentOnly { get; set; }
    public string Combat { get; set; } = "untouched";
}

public sealed class NightPolicyResult
{
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }
    public string? Message { get; set; }
    public bool DryRun { get; set; }
    public string Policy { get; set; } = NightPolicies.Human;
    public NightPolicyAppliedDto Applied { get; set; } = new();
}

public sealed class DebugActionResponse
{
    public bool Ok { get; set; } = true;
    public string Action { get; set; } = "";
    public bool Applied { get; set; }
}