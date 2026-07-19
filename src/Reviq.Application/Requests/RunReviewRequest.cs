using Reviq.Domain.Enums;

namespace Reviq.Application.Requests;

public sealed record RunReviewRequest(
    string RepoPath,
    DiffScope DiffScope = DiffScope.LastCommit,
    string? CommitHash = null,
    List<string>? Files = null,
    List<string>? Categories = null);
