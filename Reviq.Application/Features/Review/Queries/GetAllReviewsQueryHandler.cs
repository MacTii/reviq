using Mediator;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.Features.Reviews.Queries;

public sealed class GetAllReviewsQueryHandler(IReviewRepository repository)
    : IRequestHandler<GetAllReviewsQuery, IReadOnlyList<ReviewResult>>
{
    public async ValueTask<IReadOnlyList<ReviewResult>> Handle(
        GetAllReviewsQuery query, CancellationToken ct)
        => await repository.GetAllAsync(query.Limit);
}