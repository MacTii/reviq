using Microsoft.Extensions.Logging;
using Reviq.Domain.Enums;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.Configuration;

namespace Reviq.Infrastructure.AI.Providers;

public sealed class OpenAIProvider : OpenAICompatibleBase
{
    public override ProviderName Name => ProviderName.OpenAI;

    public OpenAIProvider(HttpClient httpClient, ILogger<OpenAIProvider> logger, ProviderConfig config)
        : base(httpClient, logger, config.BaseUrl, config.ApiKey)
        => CurrentModel = config.DefaultModel;
}