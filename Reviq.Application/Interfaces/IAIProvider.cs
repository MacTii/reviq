namespace Reviq.Application.Interfaces;

public interface IAIProvider
{
    string CurrentModel { get; }
    void SetModel(string model);
    Task<string> ReviewCodeAsync(string code, string language, string filePath);
    Task<bool> IsAvailableAsync();
    Task<List<string>> GetAvailableModelsAsync();
}