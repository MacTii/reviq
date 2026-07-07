namespace Reviq.Infrastructure.Configuration;

public sealed class OllamaOptions
{
    public const string Section = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string DefaultModel { get; init; } = "qwen2.5-coder:14b";
    public int TimeoutMinutes { get; init; } = 15;
}