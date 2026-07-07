using FluentValidation;
using Reviq.Application.Features.Git.Queries;

namespace Reviq.Application.Features.Git.Validators;

public sealed class GetRepoInfoQueryValidator : AbstractValidator<GetRepoInfoQuery>
{
    public GetRepoInfoQueryValidator()
    {
        RuleFor(x => x.RepoPath)
            .NotEmpty().WithMessage("RepoPath is required.")
            .MaximumLength(500).WithMessage("RepoPath too long.");
    }
}
