using Reviq.Application.Features.Webhook;

namespace Reviq.Application.Tests.Features.Webhook;

public class WebhookCommentBuilderTests
{
    [Fact]
    public void Build_NoResults_ShowsOverallScoreHundred()
    {
        var comment = WebhookCommentBuilder.Build([]);

        Assert.Contains("100/100", comment);
    }

    [Fact]
    public void Build_IncludesEachFileAndAveragesScore()
    {
        var results = new List<WebhookFileResult>
        {
            new("a.cs", 80, []),
            new("b.cs", 60, [])
        };

        var comment = WebhookCommentBuilder.Build(results);

        Assert.Contains("a.cs", comment);
        Assert.Contains("b.cs", comment);
        Assert.Contains("70/100", comment); // average of 80 and 60
    }

    [Fact]
    public void Build_FileWithNoIssues_ShowsNoIssuesFoundMessage()
    {
        var results = new List<WebhookFileResult> { new("a.cs", 100, []) };

        var comment = WebhookCommentBuilder.Build(results);

        Assert.Contains("No issues found.", comment);
    }

    [Fact]
    public void Build_FileWithIssues_ListsEachIssueTitle()
    {
        var results = new List<WebhookFileResult>
        {
            new("a.cs", 50, [new WebhookIssue("Critical", "Null deref", 42)])
        };

        var comment = WebhookCommentBuilder.Build(results);

        Assert.Contains("Null deref", comment);
        Assert.Contains("L42", comment);
    }
}
