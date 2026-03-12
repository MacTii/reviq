using Reviq.Domain.Enums;

namespace Reviq.Application.UseCases.GetRepoInfo;

public class GetRepoInfoQuery
{
    public string RepoPath { get; init; } = "";
    public DiffScope Scope { get; init; } = DiffScope.LastCommit;
    public string? CommitHash { get; init; }
}