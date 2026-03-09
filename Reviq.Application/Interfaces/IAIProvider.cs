namespace Reviq.Application.Interfaces;

public interface IAIProvider
{
    Task<string> ReviewCodeAsync(string code, string language, string filePath);
    Task<bool> IsAvailableAsync();
    Task<List<string>> GetAvailableModelsAsync();
    string CurrentModel { get; }
}
