using Mediator;
using Reviq.Application.Features.Webhook.Commands;
using Reviq.Domain.Entities;

namespace Reviq.API.Webhooks;

// Drains the webhook queue in the background, capping concurrent AI reviews so a burst of
// PR webhooks can't overwhelm the configured AI provider. Each item gets its own DI scope
// (IMediator and its dependencies are scoped, and there's no HTTP request scope to borrow
// here — see WebhookController for why that matters).
public sealed class WebhookProcessingService(
    IWebhookQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookProcessingService> logger) : BackgroundService
{
    private const int MaxConcurrency = 2;
    private readonly SemaphoreSlim _semaphore = new(MaxConcurrency);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            WebhookPayload payload;
            try
            {
                payload = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await _semaphore.WaitAsync(stoppingToken);
            _ = ProcessAsync(payload, stoppingToken);
        }
    }

    private async Task ProcessAsync(WebhookPayload payload, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new HandleWebhookCommand(payload), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process {Platform} webhook for {Repo}#{PrNumber}",
                payload.Platform, payload.RepoFullName, payload.PrNumber);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
