using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;
using Reviq.Domain.Enums;

namespace Reviq.Infrastructure.AI;

public class AIProviderFactory : IAIProviderFactory
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OllamaProvider _ollama;

    private IAIProvider _current;

    private static readonly ProviderMeta[] Providers =
    {
        new(ProviderName.Ollama,      ProviderType.Local, RequiredConfig.None),
        new(ProviderName.Claude,      ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.OpenAI,      ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.Groq,        ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.OpenRouter,  ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.LMStudio,    ProviderType.Local, RequiredConfig.BaseUrl),
    };

    public AIProviderFactory(OllamaProvider ollama, IConfiguration config, ILoggerFactory loggerFactory)
    {
        _ollama = ollama;
        _config = config;
        _loggerFactory = loggerFactory;
        _current = ollama;
    }

    public IAIProvider GetCurrent() => _current;

    public IAIProvider GetProvider(ProviderName name) =>
        name == ProviderName.Ollama ? _ollama : Build(name);

    public void SetCurrent(ProviderName name) =>
        _current = name == ProviderName.Ollama ? _ollama : Build(name);

    public IEnumerable<ProviderName> GetAvailableProviders() =>
        Providers.Select(p => p.Name);

    public IEnumerable<ProviderInfo> GetConfiguredProviders() =>
        Providers.Select(p =>
        {
            var key = _config[$"AI:{p.Name}:ApiKey"] ?? "";
            var url = _config[$"AI:{p.Name}:BaseUrl"] ?? "";
            var hasConfig = p.Required switch
            {
                RequiredConfig.None => true,
                RequiredConfig.ApiKey => !string.IsNullOrWhiteSpace(key),
                RequiredConfig.BaseUrl => !string.IsNullOrWhiteSpace(url),
                _ => false
            };
            return new ProviderInfo(p.Name, p.Type, url, hasConfig);
        });

    private IAIProvider Build(ProviderName name)
    {
        var key = _config[$"AI:{name}:ApiKey"] ?? "";
        var url = _config[$"AI:{name}:BaseUrl"] ?? "";

        return name switch
        {
            ProviderName.Claude => new ClaudeProvider(new HttpClient(), _loggerFactory.CreateLogger<ClaudeProvider>(), key),
            ProviderName.OpenAI => new OpenAIProvider(new HttpClient(), _loggerFactory.CreateLogger<OpenAIProvider>(), key),
            ProviderName.Groq => new GroqProvider(new HttpClient(), _loggerFactory.CreateLogger<GroqProvider>(), key),
            ProviderName.OpenRouter => new OpenRouterProvider(new HttpClient(), _loggerFactory.CreateLogger<OpenRouterProvider>(), key),
            ProviderName.LMStudio => new LMStudioProvider(new HttpClient(), _loggerFactory.CreateLogger<LMStudioProvider>(), url),
            _ => throw new ArgumentException($"Unknown provider: {name}")
        };
    }

    private record ProviderMeta(ProviderName Name, ProviderType Type, RequiredConfig Required);
}

public enum RequiredConfig { None, ApiKey, BaseUrl }