using Mediator;
using Reviq.Application.Common;
using Reviq.Application.DTOs;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.Features.Reviews.Commands;

public sealed class RunCodeReviewCommandHandler(
    IAIProviderFactory aiProviderFactory,
    IReviewRepository repository)
    : IRequestHandler<RunCodeReviewCommand, ReviewResultDto>
{
    public async ValueTask<ReviewResultDto> Handle(RunCodeReviewCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;
        var raw = await aiProviderFactory.GetCurrent().ReviewCodeAsync(req.Code, req.Language, req.FileName);
        var review = AIResponseParser.Parse(raw, req.FileName, req.Language);
        var reviews = new List<FileReview> { review };
        var summary = ReviewSummaryBuilder.Build(reviews);
        var result = new ReviewResult(req.FileName, "snippet", reviews, summary);

        await repository.SaveAsync(result);
        return ReviewResultDto.FromDomain(result);
    }
}