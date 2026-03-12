using Reviq.Domain.Enums;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Reviq.Infrastructure.AI;

public class OpenRouterProvider : OpenAICompatibleBase
{
    public override ProviderName Name => ProviderName.OpenRouter;

    public OpenRouterProvider(HttpClient httpClient, ILogger<OpenRouterProvider> logger, string apiKey)
        : base(httpClient, logger, "https://openrouter.ai/api/v1", apiKey)
    {
        CurrentModel = "meta-llama/llama-3.1-8b-instruct:free";
        // OpenRouter wymaga dodatkowego headera
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", "https://reviq.app");
    }
}