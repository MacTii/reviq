using System.Text.Json;

namespace Reviq.Application.Features.Webhook;

public sealed record WebhookIssue(string Severity, string Title, int? Line);

public sealed record WebhookFileResult(string FileName, int Score, IReadOnlyList<WebhookIssue> Issues);

public static class WebhookReviewParser
{
    public static (int score, IReadOnlyList<WebhookIssue> issues) Parse(string rawJson)
    {
        try
        {
            var json = rawJson.Trim();
            if (json.StartsWith("```"))
                json = string.Join('\n', json.Split('\n').Skip(1)).TrimEnd('`').Trim();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var score = root.TryGetProperty("score", out var s) ? s.GetInt32() : 70;
            var issues = new List<WebhookIssue>();

            if (root.TryGetProperty("issues", out var arr))
                foreach (var el in arr.EnumerateArray())
                    issues.Add(ParseIssue(el));

            return (Math.Clamp(score, 0, 100), issues);
        }
        catch
        {
            return (70, Array.Empty<WebhookIssue>());
        }
    }

    private static WebhookIssue ParseIssue(JsonElement el)
    {
        var severity = el.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "Info" : "Info";
        var title = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var line = el.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.Number
            ? l.GetInt32() : (int?)null;
        return new WebhookIssue(severity, title, line);
    }
}
