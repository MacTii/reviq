using Reviq.Domain.Entities;

namespace Reviq.API.Webhooks;

public interface IWebhookQueue
{
    ValueTask EnqueueAsync(WebhookPayload payload, CancellationToken ct = default);
    ValueTask<WebhookPayload> DequeueAsync(CancellationToken ct);
}
