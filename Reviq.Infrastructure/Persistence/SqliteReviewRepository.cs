using Microsoft.EntityFrameworkCore;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;
using Reviq.Infrastructure.Persistence.Entities;

namespace Reviq.Infrastructure.Persistence;

public sealed class SqliteReviewRepository(ReviqDbContext db) : IReviewRepository
{
    public async Task SaveAsync(ReviewResult result)
    {
        db.Reviews.Add(ToRecord(result));
        await db.SaveChangesAsync();
    }

    public async Task<ReviewResult?> GetByIdAsync(string reviewId)
    {
        var record = await db.Reviews
            .Include(r => r.Files).ThenInclude(f => f.Issues)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId);

        return record is null ? null : ToDomain(record);
    }

    public async Task<List<ReviewResult>> GetAllAsync(int limit = 20)
    {
        var records = await db.Reviews
            .Include(r => r.Files).ThenInclude(f => f.Issues)
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return records.Select(ToDomain).ToList();
    }

    private static ReviewResultRecord ToRecord(ReviewResult r) => new()
    {
        ReviewId = r.ReviewId,
        CreatedAt = r.CreatedAt,
        Label = r.Label,
        Source = r.Source,
        RepoPath = r.RepoPath,
        Branch = r.Branch,
        CommitHash = r.CommitHash,
        TotalIssues = r.Summary.TotalIssues,
        Critical = r.Summary.Critical,
        Warnings = r.Summary.Warnings,
        Info = r.Summary.Info,
        OverallScore = r.Summary.OverallScore,
        GeneralFeedback = r.Summary.GeneralFeedback,
        Files = r.Files.Select(f => new FileReviewRecord
        {
            ReviewResultId = r.ReviewId,
            FilePath = f.FilePath,
            Language = f.Language,
            Score = f.Score,
            Issues = f.Issues.Select(i => new ReviewIssueRecord
            {
                Severity = i.Severity,
                Category = i.Category,
                Title = i.Title,
                Description = i.Description,
                Line = i.Line,
                Suggestion = i.Suggestion,
                CodeBefore = i.CodeBefore,
                CodeAfter = i.CodeAfter
            }).ToList()
        }).ToList()
    };

    private static ReviewResult ToDomain(ReviewResultRecord r)
    {
        var summary = new ReviewSummary(r.TotalIssues, r.Critical, r.Warnings, r.Info, r.OverallScore, r.GeneralFeedback);

        var files = r.Files.Select(f => new FileReview(
            f.FilePath, f.Language, f.Score,
            f.Issues.Select(i => new ReviewIssue(
                i.Severity, i.Category, i.Title, i.Description,
                i.Line, i.Suggestion, i.CodeBefore, i.CodeAfter))
                .ToList()))
            .ToList();

        return new ReviewResult(r.ReviewId, r.CreatedAt, r.Label, r.Source, files, summary,
            r.RepoPath, r.Branch, r.CommitHash);
    }
}
