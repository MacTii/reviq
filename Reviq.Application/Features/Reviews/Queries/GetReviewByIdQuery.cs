using Mediator;
using Reviq.Domain.Entities;

namespace Reviq.Application.Features.Reviews.Queries;

public sealed record GetReviewByIdQuery(string ReviewId) : IRequest<ReviewResult?>;