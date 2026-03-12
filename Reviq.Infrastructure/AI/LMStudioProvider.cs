using Reviq.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Reviq.Infrastructure.AI;

public class LMStudioProvider : OpenAICompatibleBase
{
    public override ProviderName Name => ProviderName.LMStudio;

    public LMStudioProvider(HttpClient httpClient, ILogger<LMStudioProvider> logger, string baseUrl)
        : base(httpClient, logger, baseUrl, apiKey: "")
    {
        CurrentModel = "local-model";
    }
}