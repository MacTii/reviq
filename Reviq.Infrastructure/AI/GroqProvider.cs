using Microsoft.Extensions.Logging;

namespace Reviq.Infrastructure.AI;

public class GroqProvider : OpenAICompatibleBase
{
    public override string ProviderName => "Groq";

    public GroqProvider(HttpClient httpClient, ILogger<GroqProvider> logger, string apiKey)
        : base(httpClient, logger, "https://api.groq.com/openai/v1", apiKey)
    {
        CurrentModel = "llama-3.3-70b-versatile";
    }
}