using Reviq.Application.DTOs;

namespace Reviq.Application.UseCases.RunReview;

public class RunReviewCommand
{
    public ReviewRequestDto Request { get; init; } = new();
}
