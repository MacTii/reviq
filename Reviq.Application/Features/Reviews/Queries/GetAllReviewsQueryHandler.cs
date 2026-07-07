using Mediator;
using Reviq.Application.DTOs;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.Features.Reviews.Queries;

public sealed class GetAllReviewsQueryHandler(IReviewRepository repository)
    : IRequestHandler<GetAllReviewsQuery, IReadOnlyList<ReviewListItemDto>>
{
    public async ValueTask<IReadOnlyList<ReviewListItemDto>> Handle(
        GetAllReviewsQuery query, CancellationToken ct)
    {
        var results = await repository.GetAllAsync(query.Limit);
        return results.Select(ReviewListItemDto.FromDomain).ToList();
    }
}
