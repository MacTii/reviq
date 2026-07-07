using Reviq.Domain.Entities;

namespace Reviq.Application.DTOs;

public sealed class ReviewResultDto
{
    public string ReviewId { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string RepoPath { get; init; } = "";
    public string Branch { get; init; } = "";
    public string CommitHash { get; init; } = "";
    public IReadOnlyList<FileReview> Files { get; init; } = Array.Empty<FileReview>();
    public ReviewSummary Summary { get; init; } = default!;

    public static ReviewResultDto FromDomain(ReviewResult r) => new()
    {
        ReviewId = r.ReviewId,
        CreatedAt = r.CreatedAt,
        RepoPath = r.RepoPath,
        Branch = r.Branch,
        CommitHash = r.CommitHash,
        Files = r.Files,
        Summary = r.Summary
    };
}
