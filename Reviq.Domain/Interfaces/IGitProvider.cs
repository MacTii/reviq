using Reviq.Domain.Entities;

namespace Reviq.Domain.Interfaces;

public interface IGitProvider
{
    Task<RepoInfo> GetRepoInfoAsync(string repoPath, string? commitHash = null);
    Task<Dictionary<string, string>> GetFileContentsAsync(string repoPath, List<string> files);
    string DetectLanguage(string filePath);
}
