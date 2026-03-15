using FluentValidation;
using Reviq.Application.Features.Webhook.Commands;

namespace Reviq.Application.Features.Webhook.Validators;

public sealed class HandleWebhookCommandValidator : AbstractValidator<HandleWebhookCommand>
{
    public HandleWebhookCommandValidator()
    {
        RuleFor(x => x.Payload.Platform)
            .NotEmpty()
            .Must(p => p is "github" or "gitlab")
            .WithMessage("Platform must be 'github' or 'gitlab'.");

        RuleFor(x => x.Payload.RepoFullName).NotEmpty();
        RuleFor(x => x.Payload.PrNumber).GreaterThan(0);
    }
}
