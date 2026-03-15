using Mediator;
using Microsoft.Extensions.Logging;
using Reviq.Application.Common;
using Reviq.Application.DTOs;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;
using Reviq.Domain.Enums;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.Features.Reviews.Commands;

public sealed class RunReviewCommandHandler(
    IAIProviderFactory aiProviderFactory,
    IGitProvider gitProvider,
    IReviewRepository repository,
    ILogger<RunReviewCommandHandler> logger)
    : IRequestHandler<RunReviewCommand, ReviewResultDto>
{
    public async ValueTask<ReviewResultDto> Handle(RunReviewCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var info = await gitProvider.GetRepoInfoAsync(req.RepoPath, req.DiffScope, req.CommitHash);

        if (!info.IsValid)
            throw new InvalidOperationException(info.Error ?? "Invalid repository.");

        var filesToReview = req.Files?.Count > 0 ? req.Files : info.ChangedFiles.ToList();
        var contents = await gitProvider.GetFileContentsAsync(req.RepoPath, filesToReview);
        var categories = req.Categories ?? new List<string>();
        var fileReviews = await ReviewFilesAsync(contents, categories, ct);
        var summary = ReviewSummaryBuilder.Build(fileReviews);
        var label = $"{Path.GetFileName(req.RepoPath)} ({info.Branch})";
        var result = new ReviewResult(label, "repo", fileReviews, summary,
                                req.RepoPath, info.Branch, info.LatestCommit);

        await repository.SaveAsync(result);
        return ReviewResultDto.FromDomain(result);
    }

    private async Task<IReadOnlyList<FileReview>> ReviewFilesAsync(
        Dictionary<string, string> contents, List<string> categories, CancellationToken ct)
    {
        var reviews = new List<FileReview>();
        foreach (var (filePath, code) in contents)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogInformation("Reviewing {FilePath}...", filePath);
            var language = gitProvider.DetectLanguage(filePath);
            try
            {
                var raw = await aiProviderFactory.GetCurrent().ReviewCodeAsync(code, language, filePath, categories);
                reviews.Add(AIResponseParser.Parse(raw, filePath, language));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to review {FilePath}", filePath);
                reviews.Add(AIResponseParser.BuildFallback(filePath, language, ex.Message));
            }
        }
        return reviews;
    }
}