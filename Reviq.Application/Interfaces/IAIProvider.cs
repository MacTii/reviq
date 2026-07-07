using Reviq.Domain.Enums;

namespace Reviq.Application.Interfaces;

public interface IAIProvider
{
    ProviderName Name { get; }
    string CurrentModel { get; }
    Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null);
    Task<bool> IsAvailableAsync();
    Task<List<string>> GetAvailableModelsAsync();
}