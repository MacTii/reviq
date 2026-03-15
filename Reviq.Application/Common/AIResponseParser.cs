using Reviq.Domain.Entities;
using Reviq.Domain.Enums;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reviq.Application.Common;

public static class AIResponseParser
{
    public static FileReview Parse(string rawJson, string filePath, string language)
    {
        var json = ExtractJson(rawJson);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var issues = new List<ReviewIssue>();

            if (root.TryGetProperty("issues", out var issuesProp))
                foreach (var el in issuesProp.EnumerateArray())
                    issues.Add(ParseIssue(el));

            var score = CalculateScore(issues);
            return new FileReview(filePath, language, score, issues);
        }
        catch
        {
            return BuildFallback(filePath, language, "Failed to parse AI response. Check logs for raw output.");
        }
    }

    public static FileReview BuildFallback(string filePath, string language, string message) =>
        new(filePath, language, 0, new List<ReviewIssue>
        {
            new ReviewIssue(IssueSeverity.Warning, IssueCategory.Bug,
                "Review unavailable", message)
        });

    private static ReviewIssue ParseIssue(JsonElement el)
    {
        var severity = ParseEnum<IssueSeverity>(el, "severity");
        var category = ParseEnum<IssueCategory>(el, "category");
        var title = GetString(el, "title");
        var desc = GetString(el, "description");
        var line = el.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.Number
                         ? l.GetInt32() : (int?)null;
        var suggestion = el.TryGetProperty("suggestion", out var sg) ? sg.GetString() : null;
        var before = el.TryGetProperty("codeBefore", out var cb) ? cb.GetString() : null;
        var after = el.TryGetProperty("codeAfter", out var ca) ? ca.GetString() : null;

        // Nullify diff if identical or not valid code
        if (string.Equals(before?.Trim(), after?.Trim(), StringComparison.Ordinal)
            || !IsLikelyCode(before) || !IsLikelyCode(after))
        {
            before = null;
            after = null;
        }

        return new ReviewIssue(severity, category, title, desc, line, suggestion, before, after);
    }

    private static string ExtractJson(string raw)
    {
        var text = raw.Trim();

        // Handle ```json ... ``` blocks
        var mdMatch = Regex.Match(text, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (mdMatch.Success) return mdMatch.Groups[1].Value.Trim();

        if (text.StartsWith("```"))
        {
            var lines = text.Split('\n').ToList();
            lines.RemoveAt(0);
            if (lines.LastOrDefault()?.TrimEnd() == "```") lines.RemoveAt(lines.Count - 1);
            text = string.Join('\n', lines).Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) text = text[start..(end + 1)];
        else if (start >= 0) text = text[start..];

        if (!IsValidJson(text))
        {
            var cutPoint = Math.Max(text.LastIndexOf("},"), text.LastIndexOf('}'));
            if (cutPoint > 0)
            {
                text = text[..(cutPoint + 1)];
                var openBraces = text.Count(c => c == '{') - text.Count(c => c == '}');
                var openBrackets = text.Count(c => c == '[') - text.Count(c => c == ']');
                text += new string(']', Math.Max(0, openBrackets));
                text += new string('}', Math.Max(0, openBraces));
            }
        }

        return text;
    }

    private static bool IsValidJson(string text)
    {
        try { JsonDocument.Parse(text); return true; }
        catch { return false; }
    }

    private static bool IsLikelyCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        var t = text.Trim();
        if (t.Length < 40) return true;
        var codeChars = new[] { '{', '}', '(', ')', ';', '=', '.', '<', '>' };
        return codeChars.Any(c => t.Contains(c));
    }

    private static int CalculateScore(IList<ReviewIssue> issues)
    {
        var penalty = issues.Sum(i => i.Severity switch
        {
            IssueSeverity.Critical => 20,
            IssueSeverity.Warning => 8,
            IssueSeverity.Info => 2,
            _ => 0
        });
        return Math.Max(0, 100 - penalty);
    }

    private static T ParseEnum<T>(JsonElement el, string prop) where T : struct, Enum
    {
        if (!el.TryGetProperty(prop, out var v) || v.GetString() is not { } str) return default;
        return Enum.TryParse<T>(str, ignoreCase: true, out var result) ? result : default;
    }

    private static string GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v.GetString() ?? "" : "";
}