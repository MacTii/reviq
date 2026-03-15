namespace Reviq.Domain.Entities;

public sealed class ReviewResult
{
    public string ReviewId { get; }
    public DateTime CreatedAt { get; }
    public string Label { get; }
    public string Source { get; }
    public string RepoPath { get; }
    public string Branch { get; }
    public string CommitHash { get; }
    public IReadOnlyList<FileReview> Files { get; }
    public ReviewSummary Summary { get; }

    public ReviewResult(
        string label,
        string source,
        IReadOnlyList<FileReview> files,
        ReviewSummary summary,
        string repoPath = "",
        string branch = "",
        string commitHash = "")
    {
        ReviewId = Guid.NewGuid().ToString("N")[..8];
        CreatedAt = DateTime.UtcNow;
        Label = label;
        Source = source;
        Files = files;
        Summary = summary;
        RepoPath = repoPath;
        Branch = branch;
        CommitHash = commitHash;
    }
}
