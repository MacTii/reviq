using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;

namespace Reviq.Application.Features.Webhook;

public sealed class PrFileReviewer(
    IAIProviderFactory aiProviderFactory,
    IPrFileContentFetcher contentFetcher,
    ILogger<PrFileReviewer> logger) : IPrFileReviewer
{
    public async Task<IReadOnlyList<WebhookFileResult>> ReviewAsync(
        IEnumerable<PrFile> files, string token, CancellationToken cancellationToken = default)
    {
        var results = new List<WebhookFileResult>();

        foreach (var file in files)
        {
            var code = await contentFetcher.FetchAsync(file.RawUrl, token, cancellationToken);
            if (string.IsNullOrWhiteSpace(code)) continue;

            var language = PrFileLanguageDetector.Detect(file.FileName);
            try
            {
                var raw = await aiProviderFactory.GetCurrent().ReviewCodeAsync(code, language, file.FileName);
                var (score, issues) = WebhookReviewParser.Parse(raw);
                results.Add(new WebhookFileResult(file.FileName, score, issues));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to review {File}", file.FileName);
            }
        }

        return results;
    }
}
