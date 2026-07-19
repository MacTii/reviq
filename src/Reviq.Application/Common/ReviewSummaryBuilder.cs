using Reviq.Domain.Entities;
using Reviq.Domain.Enums;

namespace Reviq.Application.Common;

public static class ReviewSummaryBuilder
{
    public static ReviewSummary Build(IReadOnlyList<FileReview> files)
    {
        var all = files.SelectMany(f => f.Issues).ToList();
        var critical = all.Count(i => i.Severity == IssueSeverity.Critical);
        var warnings = all.Count(i => i.Severity == IssueSeverity.Warning);
        var info = all.Count(i => i.Severity == IssueSeverity.Info);
        var score = files.Count > 0 ? (int)files.Average(f => f.Score) : 100;

        var feedback = critical switch
        {
            > 5 => $"Code requires urgent attention — {critical} critical issues found.",
            > 0 => $"Found {critical} critical errors and {warnings} warnings.",
            _ => warnings > 3
                ? $"No critical errors, but {warnings} warnings require attention."
                : "Code looks solid. Minor suggestions may improve readability."
        };

        return new ReviewSummary(all.Count, critical, warnings, info, score, feedback);
    }
}