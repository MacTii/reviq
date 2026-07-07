namespace Reviq.Infrastructure.Configuration;

public sealed class AIProviderOptions
{
    public const string Section = "AI";

    public ProviderConfig Claude { get; init; } = new();
    public ProviderConfig OpenAI { get; init; } = new();
    public ProviderConfig Groq { get; init; } = new();
    public ProviderConfig OpenRouter { get; init; } = new();
    public ProviderConfig LMStudio { get; init; } = new();
}

public sealed class ProviderConfig
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "";
    public string DefaultModel { get; init; } = "";
    public string AnthropicVersion { get; init; } = "2023-06-01";
    public string Referer { get; init; } = "";
}