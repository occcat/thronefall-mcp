namespace ThronefallControl.Dto;

public static class ErrorCodes
{
    public const string IllegalPhase = "illegal_phase";
    public const string StaleId = "stale_id";
    public const string NotFound = "not_found";
    public const string InsufficientGold = "insufficient_gold";
    public const string ChoiceRequired = "choice_required";
    public const string TransitionInProgress = "transition_in_progress";
    public const string CheatDisabled = "cheat_disabled";
    public const string DryRun = "dry_run";
    public const string BindFailed = "bind_failed";
    public const string UnityException = "unity_exception";
    public const string MainThreadTimeout = "main_thread_timeout";
    public const string Unauthorized = "unauthorized";
    public const string UnsupportedInThisBuild = "unsupported_in_this_build";
}
