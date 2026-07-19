namespace Reviq.Infrastructure.Persistence.Entities;

// EF Core persistence model for ReviewResult — kept separate from the Domain entity so
// Domain stays free of ORM concerns. SqliteReviewRepository maps between the two.
public sealed class ReviewResultRecord
{
    public string ReviewId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Label { get; set; } = "";
    public string Source { get; set; } = "";
    public string RepoPath { get; set; } = "";
    public string Branch { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public int TotalIssues { get; set; }
    public int Critical { get; set; }
    public int Warnings { get; set; }
    public int Info { get; set; }
    public int OverallScore { get; set; }
    public string GeneralFeedback { get; set; } = "";
    public List<FileReviewRecord> Files { get; set; } = [];
}
