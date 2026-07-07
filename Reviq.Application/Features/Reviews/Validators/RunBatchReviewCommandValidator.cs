using FluentValidation;
using Reviq.Application.Features.Reviews.Commands;

namespace Reviq.Application.Features.Reviews.Validators;

public sealed class RunBatchReviewCommandValidator : AbstractValidator<RunBatchReviewCommand>
{
    public RunBatchReviewCommandValidator()
    {
        RuleFor(x => x.Request.Files)
            .NotEmpty().WithMessage("At least one file is required.")
            .Must(f => f.Count <= 50).WithMessage("Maximum 50 files per batch.");

        RuleForEach(x => x.Request.Files).ChildRules(file =>
        {
            file.RuleFor(f => f.Code).NotEmpty().WithMessage("File code is required.");
            file.RuleFor(f => f.FileName).NotEmpty().WithMessage("File name is required.");
        });
    }
}