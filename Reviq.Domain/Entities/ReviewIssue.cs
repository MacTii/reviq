using Reviq.Domain.Enums;

namespace Reviq.Domain.Entities;

public class ReviewIssue
{
    public IssueSeverity Severity { get; set; }
    public IssueCategory Category { get; set; }
    public int? Line { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Suggestion { get; set; }
}
