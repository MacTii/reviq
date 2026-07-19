using Reviq.Application.Common;
using Reviq.Domain.Enums;

namespace Reviq.Application.Tests.Common;

public class AIResponseParserTests
{
    [Fact]
    public void Parse_ValidJson_ReturnsIssuesAndScore()
    {
        var json = """
            {
              "issues": [
                { "severity": "Critical", "category": "Bug", "title": "Null ref", "description": "d1" },
                { "severity": "Warning", "category": "BestPractice", "title": "Naming", "description": "d2" }
              ]
            }
            """;

        var result = AIResponseParser.Parse(json, "a.cs", "C#");

        Assert.Equal("a.cs", result.FilePath);
        Assert.Equal("C#", result.Language);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal(72, result.Score); // 100 - 20 (critical) - 8 (warning)
    }

    [Fact]
    public void Parse_NoIssues_ScoreIsHundred()
    {
        var result = AIResponseParser.Parse("""{ "issues": [] }""", "a.cs", "C#");

        Assert.Empty(result.Issues);
        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void Parse_JsonWrappedInMarkdownFence_IsUnwrapped()
    {
        var json = "```json\n{ \"issues\": [{ \"severity\": \"Info\", \"category\": \"Refactor\", \"title\": \"t\", \"description\": \"d\" }] }\n```";

        var result = AIResponseParser.Parse(json, "a.cs", "C#");

        Assert.Single(result.Issues);
        Assert.Equal(IssueSeverity.Info, result.Issues[0].Severity);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsFallbackWithZeroScore()
    {
        var result = AIResponseParser.Parse("not json at all", "a.cs", "C#");

        Assert.Equal(0, result.Score);
        Assert.Single(result.Issues);
        Assert.Equal("Review unavailable", result.Issues[0].Title);
    }

    [Fact]
    public void Parse_ScoreNeverGoesBelowZero()
    {
        var json = """
            { "issues": [
              { "severity": "Critical", "category": "Bug", "title": "1", "description": "d" },
              { "severity": "Critical", "category": "Bug", "title": "2", "description": "d" },
              { "severity": "Critical", "category": "Bug", "title": "3", "description": "d" },
              { "severity": "Critical", "category": "Bug", "title": "4", "description": "d" },
              { "severity": "Critical", "category": "Bug", "title": "5", "description": "d" },
              { "severity": "Critical", "category": "Bug", "title": "6", "description": "d" }
            ] }
            """;

        var result = AIResponseParser.Parse(json, "a.cs", "C#");

        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Parse_IdenticalBeforeAfterDiff_IsNulledOut()
    {
        var json = """
            { "issues": [
              { "severity": "Info", "category": "Refactor", "title": "t", "description": "d",
                "codeBefore": "var x = 1;", "codeAfter": "var x = 1;" }
            ] }
            """;

        var result = AIResponseParser.Parse(json, "a.cs", "C#");

        Assert.Null(result.Issues[0].CodeBefore);
        Assert.Null(result.Issues[0].CodeAfter);
    }

    [Fact]
    public void BuildFallback_SetsWarningSeverityAndZeroScore()
    {
        var result = AIResponseParser.BuildFallback("a.cs", "C#", "boom");

        Assert.Equal(0, result.Score);
        Assert.Equal(IssueSeverity.Warning, result.Issues[0].Severity);
        Assert.Equal("boom", result.Issues[0].Description);
    }
}
