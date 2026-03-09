namespace Reviq.Domain.Entities;

public class ReviewResult
{
    public string ReviewId { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string RepoPath { get; set; } = "";
    public string Branch { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public List<FileReview> Files { get; set; } = new();
    public ReviewSummary Summary { get; set; } = new();
}
