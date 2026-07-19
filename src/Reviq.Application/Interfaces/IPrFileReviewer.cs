using Reviq.Application.Features.Webhook;
using Reviq.Domain.Entities;

namespace Reviq.Application.Interfaces;

public interface IPrFileReviewer
{
    Task<IReadOnlyList<WebhookFileResult>> ReviewAsync(
        IEnumerable<PrFile> files, string token, CancellationToken cancellationToken = default);
}
