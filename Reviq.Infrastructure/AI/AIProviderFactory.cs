using Reviq.Application.Interfaces;
using Reviq.Domain.Enums;
using Reviq.Domain.ValueObjects;
using Reviq.Infrastructure.Configuration;

namespace Reviq.Infrastructure.AI;

public sealed class AIProviderFactory : IAIProviderFactory
{
    private readonly IReadOnlyDictionary<ProviderName, IAIProvider> _providers;
    private readonly AIProviderOptions _options;
    private IAIProvider _current;

    private static readonly ProviderMeta[] Metas =
    {
        new(ProviderName.LocalAI,    ProviderType.Local, RequiredConfig.ModelPath),
        new(ProviderName.Ollama,     ProviderType.Local, RequiredConfig.None),
        new(ProviderName.Claude,     ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.OpenAI,     ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.Groq,       ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.OpenRouter, ProviderType.Cloud, RequiredConfig.ApiKey),
        new(ProviderName.LMStudio,   ProviderType.Local, RequiredConfig.BaseUrl),
    };

    public AIProviderFactory(IEnumerable<IAIProvider> providers, AIProviderOptions options)
    {
        _options = options;
        _providers = providers.ToDictionary(p => p.Name);
        _current = _providers.TryGetValue(ProviderName.LocalAI, out var localAI)
            ? localAI
            : _providers.Values.First();
    }

    public IAIProvider GetCurrent() => _current;

    public IAIProvider GetProvider(ProviderName name) =>
        _providers.TryGetValue(name, out var provider)
            ? provider
            : throw new ArgumentException($"Unknown provider: {name}");

    public void SetCurrent(ProviderName name) => _current = GetProvider(name);

    public void SetModel(string model)
    {
        if (_current is BaseAIProvider provider)
            provider.SetModel(model);
    }

    public IEnumerable<ProviderName> GetAvailableProviders() =>
        Metas.Select(m => m.Name);

    public IEnumerable<ProviderInfo> GetConfiguredProviders() =>
        Metas.Select(m =>
        {
            var cfg = GetProviderConfig(m.Name);
            var hasConfig = m.Required switch
            {
                RequiredConfig.None => true,
                RequiredConfig.ApiKey => !string.IsNullOrWhiteSpace(cfg?.ApiKey),
                RequiredConfig.BaseUrl => !string.IsNullOrWhiteSpace(cfg?.BaseUrl),
                RequiredConfig.ModelPath => true,
                _ => false
            };
            return new ProviderInfo(m.Name, m.Type, cfg?.BaseUrl ?? "", hasConfig);
        });

    private ProviderConfig? GetProviderConfig(ProviderName name) => name switch
    {
        ProviderName.Claude => _options.Claude,
        ProviderName.OpenAI => _options.OpenAI,
        ProviderName.Groq => _options.Groq,
        ProviderName.OpenRouter => _options.OpenRouter,
        ProviderName.LMStudio => _options.LMStudio,
        _ => null
    };

    private sealed record ProviderMeta(ProviderName Name, ProviderType Type, RequiredConfig Required);
}