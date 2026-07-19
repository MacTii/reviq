namespace Reviq.Infrastructure.Persistence.Entities;

public sealed class FileReviewRecord
{
    public int Id { get; set; }
    public string ReviewResultId { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Language { get; set; } = "";
    public int Score { get; set; }
    public List<ReviewIssueRecord> Issues { get; set; } = [];
}
