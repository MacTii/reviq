using Reviq.Application.Common;
using Reviq.Domain.Entities;
using Reviq.Domain.Enums;

namespace Reviq.Application.Tests.Common;

public class ReviewSummaryBuilderTests
{
    private static ReviewIssue Issue(IssueSeverity severity) =>
        new(severity, IssueCategory.Bug, "t", "d");

    [Fact]
    public void Build_NoFiles_ScoreIsHundredAndFeedbackIsPositive()
    {
        var summary = ReviewSummaryBuilder.Build([]);

        Assert.Equal(100, summary.OverallScore);
        Assert.Equal(0, summary.TotalIssues);
        Assert.Equal("Code looks solid. Minor suggestions may improve readability.", summary.GeneralFeedback);
    }

    [Fact]
    public void Build_CountsIssuesBySeverityAcrossFiles()
    {
        var files = new List<FileReview>
        {
            new("a.cs", "C#", 80, [Issue(IssueSeverity.Critical), Issue(IssueSeverity.Warning)]),
            new("b.cs", "C#", 90, [Issue(IssueSeverity.Info)])
        };

        var summary = ReviewSummaryBuilder.Build(files);

        Assert.Equal(3, summary.TotalIssues);
        Assert.Equal(1, summary.Critical);
        Assert.Equal(1, summary.Warnings);
        Assert.Equal(1, summary.Info);
        Assert.Equal(85, summary.OverallScore); // average of 80 and 90
    }

    [Fact]
    public void Build_ManyCriticalIssues_UsesUrgentFeedback()
    {
        var files = new List<FileReview>
        {
            new("a.cs", "C#", 0, Enumerable.Repeat(Issue(IssueSeverity.Critical), 6).ToList())
        };

        var summary = ReviewSummaryBuilder.Build(files);

        Assert.Contains("urgent attention", summary.GeneralFeedback);
    }

    [Fact]
    public void Build_FewCriticalIssues_MentionsCountsInFeedback()
    {
        var files = new List<FileReview>
        {
            new("a.cs", "C#", 50, [Issue(IssueSeverity.Critical), Issue(IssueSeverity.Warning)])
        };

        var summary = ReviewSummaryBuilder.Build(files);

        Assert.Contains("1 critical errors", summary.GeneralFeedback);
    }

    [Fact]
    public void Build_OnlyManyWarnings_MentionsWarningsRequireAttention()
    {
        var files = new List<FileReview>
        {
            new("a.cs", "C#", 60, Enumerable.Repeat(Issue(IssueSeverity.Warning), 4).ToList())
        };

        var summary = ReviewSummaryBuilder.Build(files);

        Assert.Contains("warnings require attention", summary.GeneralFeedback);
    }
}
