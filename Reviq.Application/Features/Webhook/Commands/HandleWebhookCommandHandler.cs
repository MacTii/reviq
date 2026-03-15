using Mediator;
using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;
using System.Text;

namespace Reviq.Application.Features.Webhook.Commands;

public sealed class HandleWebhookCommandHandler(
    IGitHostProviderFactory gitHostFactory,
    IAIProviderFactory aiProviderFactory,
    ILogger<HandleWebhookCommandHandler> logger)
    : IRequestHandler<HandleWebhookCommand>
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".go", ".rs", ".php" };

    public async ValueTask<Unit> Handle(HandleWebhookCommand command, CancellationToken cancellationToken)
    {
        var p = command.Payload;
        var gitHost = gitHostFactory.Create(p.Platform);

        if (p.Action is not ("opened" or "synchronize" or "update"))
            return Unit.Value;

        logger.LogInformation("Webhook {Platform} PR#{PrNumber} repo:{Repo}", p.Platform, p.PrNumber, p.RepoFullName);

        var files = await gitHost.GetPrFilesAsync(p.RepoFullName, p.PrNumber, p.Token);
        var supported = files
            .Where(f => f.Status != "removed" && SupportedExtensions.Contains(Path.GetExtension(f.FileName)))
            .Take(10)
            .ToList();

        if (supported.Count == 0)
        {
            await gitHost.PostReviewCommentAsync(p.RepoFullName, p.PrNumber,
                "**Reviq** — no supported files to analyze in this PR.", p.Token);
            return Unit.Value;
        }

        var results = await ReviewFilesAsync(supported, p.Token);
        var comment = BuildComment(results);

        await gitHost.PostReviewCommentAsync(p.RepoFullName, p.PrNumber, comment, p.Token);

        var overallScore = results.Count > 0 ? results.Average(r => r.score) : 100;
        await gitHost.SetCommitStatusAsync(p.RepoFullName, p.CommitSha,
            overallScore >= 70,
            $"Reviq: score {(int)overallScore}/100 · {results.Sum(r => r.issues.Count)} issues",
            p.Token);

        return Unit.Value;
    }

    private async Task<List<(string file, int score, List<string> issues)>> ReviewFilesAsync(
        IEnumerable<PrFile> files, string token)
    {
        var results = new List<(string, int, List<string>)>();

        foreach (var file in files)
        {
            var code = await FetchFileContentAsync(file.RawUrl, token);
            if (string.IsNullOrWhiteSpace(code)) continue;

            var lang = DetectLanguage(file.FileName);
            try
            {
                var raw = await aiProviderFactory.GetCurrent().ReviewCodeAsync(code, lang, file.FileName);
                var (score, issues) = ParseIssues(raw);
                results.Add((file.FileName, score, issues));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to review {File}", file.FileName);
            }
        }

        return results;
    }

    private static async Task<string> FetchFileContentAsync(string rawUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return "";
        using var client = new HttpClient();
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("Reviq");
        try { return await client.GetStringAsync(rawUrl); }
        catch { return ""; }
    }

    private static string BuildComment(List<(string file, int score, List<string> issues)> results)
    {
        var sb = new StringBuilder();
        var overall = results.Count > 0 ? (int)results.Average(r => r.score) : 100;
        var emoji = overall >= 80 ? "🟢" : overall >= 60 ? "🟡" : "🔴";

        sb.AppendLine("## 🔬 Reviq — AI Code Review");
        sb.AppendLine();
        sb.AppendLine($"**Overall score: {emoji} {overall}/100**");
        sb.AppendLine();

        foreach (var (file, score, issues) in results)
        {
            var fe = score >= 80 ? "🟢" : score >= 60 ? "🟡" : "🔴";
            sb.AppendLine($"### `{file}` — {fe} {score}/100");
            if (issues.Count == 0) sb.AppendLine("✅ No issues found.");
            else issues.ForEach(i => sb.AppendLine($"- {i}"));
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*Powered by [Reviq](https://github.com) + Local AI*");
        return sb.ToString();
    }

    private static (int score, List<string> issues) ParseIssues(string rawJson)
    {
        try
        {
            var json = rawJson.Trim();
            if (json.StartsWith("```")) json = string.Join('\n', json.Split('\n').Skip(1)).TrimEnd('`').Trim();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var score = root.TryGetProperty("score", out var s) ? s.GetInt32() : 70;
            var issues = new List<string>();

            if (root.TryGetProperty("issues", out var arr))
                foreach (var el in arr.EnumerateArray())
                {
                    var sev = el.TryGetProperty("severity", out var sv) ? sv.GetString() : "Info";
                    var title = el.TryGetProperty("title", out var t) ? t.GetString() : "";
                    var lineNum = el.TryGetProperty("line", out var l) && l.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? $"L{l.GetInt32()} " : "";
                    var sevEmoji = sev switch { "Critical" => "🔴", "Warning" => "🟡", _ => "🔵" };
                    issues.Add($"{sevEmoji} **{lineNum}{title}**");
                }

            return (Math.Clamp(score, 0, 100), issues);
        }
        catch { return (70, new()); }
    }

    private static string DetectLanguage(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".cs" => "C#",
            ".ts" or ".tsx" => "TypeScript",
            ".js" or ".jsx" => "JavaScript",
            ".py" => "Python",
            ".java" => "Java",
            ".go" => "Go",
            ".rs" => "Rust",
            ".php" => "PHP",
            _ => "Unknown"
        };
}