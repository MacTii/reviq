using Mediator;
using Reviq.Application.DTOs;

namespace Reviq.Application.Features.Reviews.Queries;

public sealed record GetAllReviewsQuery(int Limit = 50) : IRequest<IReadOnlyList<ReviewListItemDto>>;