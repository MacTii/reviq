using Mediator;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.Features.Reviews.Queries;

public sealed class GetReviewByIdQueryHandler(IReviewRepository repository)
    : IRequestHandler<GetReviewByIdQuery, ReviewResult?>
{
    public async ValueTask<ReviewResult?> Handle(
        GetReviewByIdQuery query, CancellationToken ct)
        => await repository.GetByIdAsync(query.ReviewId);
}