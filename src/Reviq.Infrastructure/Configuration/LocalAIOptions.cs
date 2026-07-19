namespace Reviq.Infrastructure.Configuration;

public sealed class LocalAIOptions
{
    public const string Section = "LocalAI";

    public string ModelsDir { get; init; } = "Models";
}