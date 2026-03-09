using Reviq.Domain.Entities;

namespace Reviq.Application.UseCases.HandleWebhook;

public class HandleWebhookCommand
{
    public WebhookPayload Payload { get; init; } = new();
}