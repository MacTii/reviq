using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;

namespace Reviq.Infrastructure.Git;

public sealed class HttpPrFileContentFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpPrFileContentFetcher> logger) : IPrFileContentFetcher
{
    public async Task<string> FetchAsync(string rawUrl, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return "";

        var client = httpClientFactory.CreateClient(nameof(HttpPrFileContentFetcher));
        using var request = new HttpRequestMessage(HttpMethod.Get, rawUrl);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch PR file content from {RawUrl}", rawUrl);
            return "";
        }
    }
}
