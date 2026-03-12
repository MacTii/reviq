using Reviq.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Reviq.Infrastructure.AI;

public class OpenAIProvider : OpenAICompatibleBase
{
    public override ProviderName Name => ProviderName.OpenAI;

    public OpenAIProvider(HttpClient httpClient, ILogger<OpenAIProvider> logger, string apiKey)
        : base(httpClient, logger, "https://api.openai.com/v1", apiKey)
    {
        CurrentModel = "gpt-4o-mini";
    }
}