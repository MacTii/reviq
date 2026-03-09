using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;

namespace Reviq.Application.UseCases.HandleWebhook;

public class HandleWebhookHandler(
    IGitHostProviderFactory gitHostFactory,
    IAIProvider aiProvider,
    ILogger<HandleWebhookHandler> logger)
{
    public async Task HandleAsync(HandleWebhookCommand command)
    {
        var p = command.Payload;
        var gitHost = gitHostFactory.Create(p.Platform);

        // Ignoruj wszystko poza opened/synchronize/update
        if (p.Action is not ("opened" or "synchronize" or "update")) return;

        logger.LogInformation("Webhook {Platform} PR#{PrNumber} repo:{Repo}",
            p.Platform, p.PrNumber, p.RepoFullName);

        // 1. Pobierz pliki z PR
        var files = await gitHost.GetPrFilesAsync(p.RepoFullName, p.PrNumber, p.Token);
        var supported = files
            .Where(f => f.Status != "removed")
            .Where(f => IsSupported(f.FileName))
            .Take(10) // limit żeby nie przekroczyć czasu
            .ToList();

        if (supported.Count == 0)
        {
            await gitHost.PostReviewCommentAsync(
                p.RepoFullName, p.PrNumber,
                "**Reviq** — brak obsługiwanych plików do analizy w tym PR.",
                p.Token);
            return;
        }

        // 2. Pobierz zawartość plików i analizuj
        var results = new List<(string file, string lang, int score, List<string> issues)>();

        foreach (var file in supported)
        {
            var code = await FetchFileContentAsync(file.RawUrl, p.Token);
            if (string.IsNullOrWhiteSpace(code)) continue;

            var lang = DetectLanguage(file.FileName);
            try
            {
                var raw = await aiProvider.ReviewCodeAsync(code, lang, file.FileName);
                var (score, issues) = ParseIssues(raw);
                results.Add((file.FileName, lang, score, issues));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to review {File}", file.FileName);
            }
        }

        // 3. Buduj komentarz
        var comment = BuildComment(results);
        await gitHost.PostReviewCommentAsync(p.RepoFullName, p.PrNumber, comment, p.Token);

        // 4. Ustaw status commita
        var overallScore = results.Count > 0 ? results.Average(r => r.score) : 100;
        var success = overallScore >= 70;
        await gitHost.SetCommitStatusAsync(
            p.RepoFullName, p.CommitSha, success,
            $"Reviq: score {(int)overallScore}/100 · {results.Sum(r => r.issues.Count)} issues",
            p.Token);
    }

    private async Task<string> FetchFileContentAsync(string rawUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return "";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("Reviq");
        try { return await client.GetStringAsync(rawUrl); }
        catch { return ""; }
    }

    private static string BuildComment(List<(string file, string lang, int score, List<string> issues)> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 🔬 Reviq — AI Code Review");
        sb.AppendLine();

        var overall = results.Count > 0 ? (int)results.Average(r => r.score) : 100;
        var emoji = overall >= 80 ? "🟢" : overall >= 60 ? "🟡" : "🔴";
        sb.AppendLine($"**Overall score: {emoji} {overall}/100**");
        sb.AppendLine();

        foreach (var (file, lang, score, issues) in results)
        {
            var fe = score >= 80 ? "🟢" : score >= 60 ? "🟡" : "🔴";
            sb.AppendLine($"### `{file}` — {fe} {score}/100");
            if (issues.Count == 0)
            {
                sb.AppendLine("✅ Brak problemów.");
            }
            else
            {
                foreach (var issue in issues)
                    sb.AppendLine($"- {issue}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*Powered by [Reviq](https://github.com) + Ollama (local AI)*");
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
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var sev = el.TryGetProperty("severity", out var sv) ? sv.GetString() : "Info";
                    var title = el.TryGetProperty("title", out var t) ? t.GetString() : "";
                    var line = el.TryGetProperty("line", out var l) && l.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? $"L{l.GetInt32()} " : "";
                    var sevEmoji = sev switch { "Critical" => "🔴", "Warning" => "🟡", _ => "🔵" };
                    issues.Add($"{sevEmoji} **{line}{title}**");
                }
            }
            return (Math.Clamp(score, 0, 100), issues);
        }
        catch { return (70, new()); }
    }

    private static bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower();
        return ext is ".cs" or ".ts" or ".tsx" or ".js" or ".jsx"
                   or ".py" or ".java" or ".go" or ".rs" or ".php";
    }

    private static string DetectLanguage(string fileName) =>
        Path.GetExtension(fileName).ToLower() switch
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