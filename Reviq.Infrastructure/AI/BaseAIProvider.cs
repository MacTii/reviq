using Reviq.Application.Interfaces;
using Reviq.Domain.Enums;
using Reviq.Infrastructure.AI.Parsing;

namespace Reviq.Infrastructure.AI;

public abstract class BaseAIProvider : IAIProvider
{
    public abstract ProviderName Name { get; }
    public string CurrentModel { get; protected set; } = "";

    internal virtual void SetModel(string model) => CurrentModel = model;

    public abstract Task<bool> IsAvailableAsync();
    public abstract Task<List<string>> GetAvailableModelsAsync();
    public abstract Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null);

    protected static string BuildPrompt(string code, string language, string filePath, IList<string>? categories)
        => PromptBuilder.Build(code, language, filePath, categories);
}