using Reviq.Application.Features.Webhook;

namespace Reviq.Application.Tests.Features.Webhook;

public class WebhookReviewParserTests
{
    [Fact]
    public void Parse_ValidJson_ReturnsScoreAndIssues()
    {
        var json = """
            { "score": 65, "issues": [
              { "severity": "Critical", "title": "SQL injection", "line": 12 },
              { "severity": "Info", "title": "Missing doc" }
            ] }
            """;

        var (score, issues) = WebhookReviewParser.Parse(json);

        Assert.Equal(65, score);
        Assert.Equal(2, issues.Count);
        Assert.Equal("Critical", issues[0].Severity);
        Assert.Equal(12, issues[0].Line);
        Assert.Null(issues[1].Line);
    }

    [Fact]
    public void Parse_ScoreClampedToZeroAndHundred()
    {
        var (scoreTooHigh, _) = WebhookReviewParser.Parse("""{ "score": 150, "issues": [] }""");
        var (scoreTooLow, _) = WebhookReviewParser.Parse("""{ "score": -20, "issues": [] }""");

        Assert.Equal(100, scoreTooHigh);
        Assert.Equal(0, scoreTooLow);
    }

    [Fact]
    public void Parse_WrappedInMarkdownFence_IsUnwrapped()
    {
        var json = "```json\n{ \"score\": 80, \"issues\": [] }\n```";

        var (score, issues) = WebhookReviewParser.Parse(json);

        Assert.Equal(80, score);
        Assert.Empty(issues);
    }

    [Fact]
    public void Parse_InvalidJson_FallsBackTo70WithNoIssues()
    {
        var (score, issues) = WebhookReviewParser.Parse("not json");

        Assert.Equal(70, score);
        Assert.Empty(issues);
    }

    [Fact]
    public void Parse_MissingScore_DefaultsTo70()
    {
        var (score, _) = WebhookReviewParser.Parse("""{ "issues": [] }""");

        Assert.Equal(70, score);
    }
}
