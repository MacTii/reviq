using System.Text;

namespace Reviq.Application.Features.Webhook;

public static class WebhookCommentBuilder
{
    public static string Build(IReadOnlyList<WebhookFileResult> results)
    {
        var sb = new StringBuilder();
        var overall = results.Count > 0 ? (int)results.Average(r => r.Score) : 100;
        var emoji = ScoreEmoji(overall);

        sb.AppendLine("## 🔬 Reviq — AI Code Review");
        sb.AppendLine();
        sb.AppendLine($"**Overall score: {emoji} {overall}/100**");
        sb.AppendLine();

        foreach (var result in results)
        {
            sb.AppendLine($"### `{result.FileName}` — {ScoreEmoji(result.Score)} {result.Score}/100");
            if (result.Issues.Count == 0)
                sb.AppendLine("✅ No issues found.");
            else
                foreach (var issue in result.Issues)
                    sb.AppendLine($"- {SeverityEmoji(issue.Severity)} **{(issue.Line is { } l ? $"L{l} " : "")}{issue.Title}**");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*Powered by [Reviq](https://github.com) + Local AI*");
        return sb.ToString();
    }

    private static string ScoreEmoji(int score) => score >= 80 ? "🟢" : score >= 60 ? "🟡" : "🔴";

    private static string SeverityEmoji(string severity) => severity switch
    {
        "Critical" => "🔴",
        "Warning" => "🟡",
        _ => "🔵"
    };
}
