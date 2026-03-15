using FluentValidation;
using Reviq.Application.Features.Reviews.Commands;

namespace Reviq.Application.Features.Reviews.Validators;

public sealed class RunReviewCommandValidator : AbstractValidator<RunReviewCommand>
{
    public RunReviewCommandValidator()
    {
        RuleFor(x => x.Request.RepoPath)
            .NotEmpty().WithMessage("RepoPath is required.")
            .MaximumLength(500).WithMessage("RepoPath too long.");
    }
}