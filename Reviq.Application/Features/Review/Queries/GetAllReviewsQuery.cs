using Mediator;
using Reviq.Domain.Entities;

namespace Reviq.Application.Features.Reviews.Queries;

public sealed record GetAllReviewsQuery(int Limit = 50) : IRequest<IReadOnlyList<ReviewResult>>;