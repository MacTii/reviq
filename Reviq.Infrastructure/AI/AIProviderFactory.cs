using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Infrastructure.AI;

public class AIProviderFactory : IAIProviderFactory
{
    private readonly OllamaProvider _ollamaProvider;
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;

    private string _currentProviderName = "ollama";
    private IAIProvider? _currentProvider;

    public AIProviderFactory(OllamaProvider ollamaProvider, IConfiguration config, ILoggerFactory loggerFactory)
    {
        _ollamaProvider = ollamaProvider;
        _config = config;
        _loggerFactory = loggerFactory;
        _currentProvider = ollamaProvider;
    }

    public IAIProvider GetCurrent() => _currentProvider ?? _ollamaProvider;

    public IAIProvider GetProvider(string name) =>
        name == "ollama" ? _ollamaProvider : BuildExternalProvider(name);

    public void SetCurrent(string providerName, string? apiKey = null, string? baseUrl = null)
    {
        _currentProviderName = providerName;
        _currentProvider = providerName == "ollama"
            ? _ollamaProvider
            : BuildExternalProvider(providerName, apiKey, baseUrl);
    }

    public IEnumerable<string> GetAvailableProviders() =>
        new[] { "ollama", "openai", "groq", "openrouter", "lmstudio" };

    public IEnumerable<ProviderInfo> GetConfiguredProviders()
    {
        var list = new List<ProviderInfo>
        {
            new("ollama", "Ollama", "local", _config["Ollama:BaseUrl"] ?? "http://localhost:11434")
        };

        foreach (var name in new[] { "openai", "groq", "openrouter", "lmstudio" })
        {
            var key = _config[$"AI:{name}:ApiKey"] ?? "";
            var baseUrl = _config[$"AI:{name}:BaseUrl"] ?? "";
            var hasConfig = name == "lmstudio"
                ? !string.IsNullOrWhiteSpace(baseUrl)
                : !string.IsNullOrWhiteSpace(key);

            list.Add(new(name, ProviderLabel(name), name == "lmstudio" ? "local" : "cloud", baseUrl, hasConfig));
        }

        return list;
    }

    private OpenAICompatibleProvider BuildExternalProvider(string name, string? apiKey = null, string? baseUrl = null)
    {
        var resolvedKey = apiKey ?? _config[$"AI:{name}:ApiKey"] ?? "";
        var resolvedUrl = baseUrl ?? _config[$"AI:{name}:BaseUrl"] ?? "";
        var http = new HttpClient();
        var logger = _loggerFactory.CreateLogger<OpenAICompatibleProvider>();
        return new OpenAICompatibleProvider(http, logger, name, resolvedUrl, resolvedKey);
    }

    private static string ProviderLabel(string name) => name switch
    {
        "openai" => "OpenAI",
        "groq" => "Groq",
        "openrouter" => "OpenRouter",
        "lmstudio" => "LM Studio",
        _ => name
    };
}