using Mediator;
using Microsoft.Extensions.Logging;
using Reviq.Application.Common;
using Reviq.Application.DTOs;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.Features.Reviews.Commands;

public sealed class RunBatchReviewCommandHandler(
    IAIProviderFactory aiProviderFactory,
    IReviewRepository repository,
    ILogger<RunBatchReviewCommandHandler> logger)
    : IRequestHandler<RunBatchReviewCommand, ReviewResultDto>
{
    public async ValueTask<ReviewResultDto> Handle(RunBatchReviewCommand command, CancellationToken cancellationToken)
    {
        var fileReviews = new List<FileReview>();

        foreach (var req in command.Request.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Reviewing {FileName}...", req.FileName);
            try
            {
                var raw = await aiProviderFactory.GetCurrent().ReviewCodeAsync(req.Code, req.Language, req.FileName);
                logger.LogDebug("[Review] Raw for {FileName}: {Raw}",
                    req.FileName, raw[..Math.Min(500, raw.Length)]);
                fileReviews.Add(AIResponseParser.Parse(raw, req.FileName, req.Language));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to review {FileName}", req.FileName);
                fileReviews.Add(AIResponseParser.BuildFallback(req.FileName, req.Language, ex.Message));
            }
        }

        var summary = ReviewSummaryBuilder.Build(fileReviews);
        var result = new ReviewResult(command.Label, "snippet", fileReviews, summary);
        await repository.SaveAsync(result);
        return ReviewResultDto.FromDomain(result);
    }
}