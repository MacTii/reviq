using Mediator;
using Reviq.Application.DTOs;
using Reviq.Application.Requests;

namespace Reviq.Application.Features.Reviews.Commands;

public sealed record RunReviewCommand(RunReviewRequest Request) : IRequest<ReviewResultDto>;