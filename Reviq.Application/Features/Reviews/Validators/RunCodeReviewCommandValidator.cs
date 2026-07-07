using FluentValidation;
using Reviq.Application.Features.Reviews.Commands;

namespace Reviq.Application.Features.Reviews.Validators;

public sealed class RunCodeReviewCommandValidator : AbstractValidator<RunCodeReviewCommand>
{
    public RunCodeReviewCommandValidator()
    {
        RuleFor(x => x.Request.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(200_000).WithMessage("Code exceeds maximum size.");

        RuleFor(x => x.Request.FileName)
            .NotEmpty().WithMessage("FileName is required.")
            .MaximumLength(260).WithMessage("FileName too long.");
    }
}