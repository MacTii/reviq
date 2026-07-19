using Reviq.Domain.Entities;

namespace Reviq.Domain.Interfaces;

public interface IGitHostProvider
{
    Task PostReviewCommentAsync(string repoFullName, int prNumber, string body, string token);
    Task SetCommitStatusAsync(string repoFullName, string commitSha, bool success, string description, string token);
    Task<List<PrFile>> GetPrFilesAsync(string repoFullName, int prNumber, string token);
}