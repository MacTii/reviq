using Microsoft.Extensions.Logging;
using Reviq.Domain.Enums;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.Configuration;

namespace Reviq.Infrastructure.AI.Providers;

public sealed class GroqProvider : OpenAICompatibleBase
{
    public override ProviderName Name => ProviderName.Groq;

    public GroqProvider(HttpClient httpClient, ILogger<GroqProvider> logger, ProviderConfig config)
        : base(httpClient, logger, config.BaseUrl, config.ApiKey)
        => CurrentModel = config.DefaultModel;
}