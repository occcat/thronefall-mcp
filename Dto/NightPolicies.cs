namespace ThronefallControl.Dto;

public static class NightPolicies
{
    public const string Human = "human";
    public const string AfkCastle = "afk_castle";
    public const string ScriptedPosts = "scripted_posts";

    public static bool TryParse(string? value, out string policy)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            policy = Human;
            return true;
        }

        switch (value.Trim().ToLowerInvariant().Replace('-', '_'))
        {
            case "human":
                policy = Human;
                return true;
            case "afk_castle":
                policy = AfkCastle;
                return true;
            case "scripted_posts":
                policy = ScriptedPosts;
                return true;
            default:
                policy = value.Trim();
                return false;
        }
    }
}
