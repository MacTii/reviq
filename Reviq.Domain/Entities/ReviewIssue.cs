using Reviq.Domain.Enums;

namespace Reviq.Domain.Entities;

public sealed class ReviewIssue
{
    public IssueSeverity Severity { get; }
    public IssueCategory Category { get; }
    public string Title { get; }
    public string Description { get; }
    public int? Line { get; }
    public string? Suggestion { get; }
    public string? CodeBefore { get; }
    public string? CodeAfter { get; }

    public ReviewIssue(
        IssueSeverity severity,
        IssueCategory category,
        string title,
        string description,
        int? line = null,
        string? suggestion = null,
        string? codeBefore = null,
        string? codeAfter = null)
    {
        Severity = severity;
        Category = category;
        Title = title;
        Description = description;
        Line = line;
        Suggestion = suggestion;
        CodeBefore = codeBefore;
        CodeAfter = codeAfter;
    }
}
