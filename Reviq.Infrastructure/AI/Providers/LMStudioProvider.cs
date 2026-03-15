using Microsoft.Extensions.Logging;
using Reviq.Domain.Enums;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.Configuration;

namespace Reviq.Infrastructure.AI.Providers;

public sealed class LMStudioProvider : OpenAICompatibleBase
{
    public override ProviderName Name => ProviderName.LMStudio;

    public LMStudioProvider(HttpClient httpClient, ILogger<LMStudioProvider> logger, ProviderConfig config)
        : base(httpClient, logger, config.BaseUrl, config.ApiKey)
        => CurrentModel = config.DefaultModel;
}