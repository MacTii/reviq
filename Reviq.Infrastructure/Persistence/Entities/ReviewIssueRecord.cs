using Reviq.Domain.Enums;

namespace Reviq.Infrastructure.Persistence.Entities;

public sealed class ReviewIssueRecord
{
    public int Id { get; set; }
    public int FileReviewId { get; set; }
    public IssueSeverity Severity { get; set; }
    public IssueCategory Category { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int? Line { get; set; }
    public string? Suggestion { get; set; }
    public string? CodeBefore { get; set; }
    public string? CodeAfter { get; set; }
}
