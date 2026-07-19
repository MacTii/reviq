using Reviq.Domain.Entities;
using Reviq.Domain.Enums;

namespace Reviq.Domain.Interfaces;

public interface IGitProvider
{
    Task<RepoInfo> GetRepoInfoAsync(string repoPath, DiffScope scope = DiffScope.LastCommit, string? commitHash = null);
    Task<Dictionary<string, string>> GetFileContentsAsync(string repoPath, List<string> files);
    string DetectLanguage(string filePath);
}
