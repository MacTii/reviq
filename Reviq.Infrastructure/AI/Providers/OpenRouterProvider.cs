using Microsoft.Extensions.Logging;
using Reviq.Domain.Enums;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.Configuration;

namespace Reviq.Infrastructure.AI.Providers;

public sealed class OpenRouterProvider : OpenAICompatibleBase
{
    public override ProviderName Name => ProviderName.OpenRouter;

    public OpenRouterProvider(HttpClient httpClient, ILogger<OpenRouterProvider> logger, ProviderConfig config)
        : base(httpClient, logger, config.BaseUrl, config.ApiKey)
    {
        CurrentModel = config.DefaultModel;
        if (!string.IsNullOrWhiteSpace(config.Referer))
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", config.Referer);
    }
}