namespace Reviq.API.Configuration;

public sealed class SecurityOptions
{
    public const string Section = "Security";

    // Empty by default = the API-key gate is disabled, matching how the AI provider
    // keys behave elsewhere in this app (see ApiKeyMiddleware).
    public string ApiKey { get; init; } = "";
}
