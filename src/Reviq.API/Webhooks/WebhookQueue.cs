using Reviq.Domain.Entities;
using System.Threading.Channels;

namespace Reviq.API.Webhooks;

// Bounded so a burst of webhooks can't grow memory unbounded — WriteAsync/EnqueueAsync
// simply awaits until there's room, which is the desired backpressure for this workload.
public sealed class WebhookQueue : IWebhookQueue
{
    private readonly Channel<WebhookPayload> _channel = Channel.CreateBounded<WebhookPayload>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

    public ValueTask EnqueueAsync(WebhookPayload payload, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(payload, ct);

    public ValueTask<WebhookPayload> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
