using Mediator;
using Reviq.Application.DTOs;
using Reviq.Application.Requests;

namespace Reviq.Application.Features.Reviews.Commands;

public sealed record RunBatchReviewCommand(BatchReviewRequest Request, string Label) : IRequest<ReviewResultDto>;