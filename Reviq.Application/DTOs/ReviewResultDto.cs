using Reviq.Domain.Entities;

namespace Reviq.Application.DTOs;

public class ReviewResultDto
{
    public string ReviewId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string RepoPath { get; set; } = "";
    public string Branch { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public List<FileReview> Files { get; set; } = new();
    public ReviewSummary Summary { get; set; } = new();

    public static ReviewResultDto FromDomain(ReviewResult result) => new()
    {
        ReviewId = result.ReviewId,
        CreatedAt = result.CreatedAt,
        RepoPath = result.RepoPath,
        Branch = result.Branch,
        CommitHash = result.CommitHash,
        Files = result.Files,
        Summary = result.Summary
    };
}
