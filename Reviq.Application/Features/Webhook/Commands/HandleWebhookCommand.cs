using Mediator;
using Reviq.Domain.Entities;

namespace Reviq.Application.Features.Webhook.Commands;

public sealed record HandleWebhookCommand(WebhookPayload Payload) : IRequest;
