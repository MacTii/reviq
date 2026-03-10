using System.Text.Json;
using Microsoft.Extensions.Logging;
using Reviq.Application.DTOs;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;
using Reviq.Domain.Enums;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.UseCases.RunReview;

public class RunReviewHandler(
    IGitProvider gitProvider,
    IAIProvider aiProvider,
    IReviewRepository reviewRepository,
    ILogger<RunReviewHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public async Task<ReviewResultDto> HandleAsync(RunReviewCommand command)
    {
        var req = command.Request;

        var repoInfo = await gitProvider.GetRepoInfoAsync(req.RepoPath, req.CommitHash);
        if (!repoInfo.IsValid)
            throw new InvalidOperationException(repoInfo.Error ?? "Invalid repository.");

        var filesToReview = req.Files.Count > 0 ? req.Files : repoInfo.ChangedFiles;
        if (filesToReview.Count == 0)
            throw new InvalidOperationException("Brak plików do przeglądu. Sprawdź czy repo ma commity.");

        var fileContents = await gitProvider.GetFileContentsAsync(req.RepoPath, filesToReview);

        var fileReviews = new List<FileReview>();
        foreach (var (filePath, code) in fileContents)
        {
            logger.LogInformation("Reviewing {FilePath}...", filePath);
            var language = gitProvider.DetectLanguage(filePath);

            try
            {
                var rawJson = await aiProvider.ReviewCodeAsync(code, language, filePath);
                var fileReview = ParseAIResponse(rawJson, filePath, language);
                fileReviews.Add(fileReview);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to review {FilePath}", filePath);
                fileReviews.Add(BuildFallbackReview(filePath, language, ex.Message));
            }
        }

        var result = new ReviewResult
        {
            RepoPath = req.RepoPath,
            Branch = repoInfo.Branch,
            CommitHash = repoInfo.LatestCommit,
            Files = fileReviews,
            Summary = BuildSummary(fileReviews)
        };

        await reviewRepository.SaveAsync(result);

        return ReviewResultDto.FromDomain(result);
    }

    public void SetModel(string model) => aiProvider.SetModel(model);

    public async Task<ReviewResultDto> HandleRawCodeAsync(CodeReviewRequestDto request)
    {
        var rawJson = await aiProvider.ReviewCodeAsync(request.Code, request.Language, request.FileName);
        var fileReview = ParseAIResponse(rawJson, request.FileName, request.Language);

        var result = new ReviewResult
        {
            RepoPath = "",
            Branch = "",
            CommitHash = "",
            Files = new List<FileReview> { fileReview },
            Summary = BuildSummary(new List<FileReview> { fileReview })
        };

        return ReviewResultDto.FromDomain(result);
    }

    private static FileReview ParseAIResponse(string rawJson, string filePath, string language)
    {
        var json = ExtractJson(rawJson);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var score = root.TryGetProperty("score", out var s) ? s.GetInt32() : 70;
            var issues = new List<ReviewIssue>();

            if (root.TryGetProperty("issues", out var issuesProp))
            {
                foreach (var el in issuesProp.EnumerateArray())
                {
                    var issue = new ReviewIssue
                    {
                        Severity = ParseEnum<IssueSeverity>(el, "severity"),
                        Category = ParseEnum<IssueCategory>(el, "category"),
                        Title = GetString(el, "title"),
                        Description = GetString(el, "description"),
                        Suggestion = el.TryGetProperty("suggestion", out var sg) ? sg.GetString() : null,
                        CodeBefore = el.TryGetProperty("codeBefore", out var cb) ? cb.GetString() : null,
                        CodeAfter = el.TryGetProperty("codeAfter", out var ca) ? ca.GetString() : null,
                    };

                    if (el.TryGetProperty("line", out var line) && line.ValueKind == JsonValueKind.Number)
                        issue.Line = line.GetInt32();

                    issues.Add(issue);
                }
            }

            return new FileReview
            {
                FilePath = filePath,
                Language = language,
                Score = Math.Clamp(score, 0, 100),
                Issues = issues
            };
        }
        catch
        {
            return BuildFallbackReview(filePath, language, "Nie udało się sparsować odpowiedzi AI.");
        }
    }

    private static ReviewSummary BuildSummary(List<FileReview> files)
    {
        var all = files.SelectMany(f => f.Issues).ToList();
        var critical = all.Count(i => i.Severity == IssueSeverity.Critical);
        var warnings = all.Count(i => i.Severity == IssueSeverity.Warning);
        var info = all.Count(i => i.Severity == IssueSeverity.Info);

        return new ReviewSummary
        {
            TotalIssues = all.Count,
            Critical = critical,
            Warnings = warnings,
            Info = info,
            OverallScore = files.Count > 0 ? (int)files.Average(f => f.Score) : 0,
            GeneralFeedback = critical switch
            {
                > 5 => $"Kod wymaga pilnej uwagi — znaleziono {critical} krytycznych problemów.",
                > 0 => $"Znaleziono {critical} krytycznych błędów i {warnings} ostrzeżeń.",
                _ when warnings > 3 => $"Brak błędów krytycznych, jednak {warnings} ostrzeżeń wymaga uwagi.",
                _ => "Kod wygląda solidnie. Drobne sugestie mogą poprawić czytelność."
            }
        };
    }

    private static FileReview BuildFallbackReview(string filePath, string language, string message) => new()
    {
        FilePath = filePath,
        Language = language,
        Score = 50,
        Issues = new List<ReviewIssue>
        {
            new()
            {
                Severity = IssueSeverity.Info,
                Category = IssueCategory.Bug,
                Title = "Review niedostępny",
                Description = message
            }
        }
    };

    private static T ParseEnum<T>(JsonElement el, string prop) where T : struct, Enum =>
        el.TryGetProperty(prop, out var v) && Enum.TryParse<T>(v.GetString(), true, out var r) ? r : default;

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? "" : "";

    /// <summary>
    /// Wyciąga poprawny JSON z odpowiedzi modelu — obsługuje markdown, tekst przed/po JSON
    /// oraz obcięte odpowiedzi (uzupełnia brakujące nawiasy).
    /// </summary>
    private static string ExtractJson(string raw)
    {
        var text = raw.Trim();

        // Usuń bloki markdown ```json ... ```
        if (text.StartsWith("```"))
        {
            var lines = text.Split('\n').ToList();
            lines.RemoveAt(0);
            if (lines.LastOrDefault()?.TrimEnd() == "```")
                lines.RemoveAt(lines.Count - 1);
            text = string.Join('\n', lines).Trim();
        }

        // Znajdź pierwszy { i wytnij od niego
        var start = text.IndexOf('{');
        if (start > 0) text = text[start..];

        // Jeśli JSON jest obcięty — spróbuj go zamknąć
        if (!IsValidJson(text))
        {
            // Znajdź ostatni kompletny issue i zamknij tablicę + obiekt
            var lastComma = text.LastIndexOf("},");
            var lastBrace = text.LastIndexOf('}');
            var cutPoint = Math.Max(lastComma, lastBrace);
            if (cutPoint > 0)
            {
                text = text[..(cutPoint + 1)];
                // Zamknij "issues" i główny obiekt
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
}