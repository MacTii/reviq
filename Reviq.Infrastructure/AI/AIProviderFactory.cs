using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;
using Reviq.Domain.Entities;

namespace Reviq.Infrastructure.AI;

public class AIProviderFactory : IAIProviderFactory
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;

    // Ollama jest wstrzyknięta przez DI bo jako jedyna ma HttpClient skonfigurowany
    // przez AddHttpClient<OllamaProvider> w Program.cs (BaseAddress, Timeout).
    // Pozostałe providery tworzone są dynamicznie w Build() bo potrzebują
    // klucza API lub URL który może się zmieniać w runtime.
    private readonly OllamaProvider _ollama;

    private IAIProvider _current;

    // Metadane providerów — nazwa wyświetlana, typ, czy wymaga klucza czy URL
    private static readonly ProviderMeta[] Providers =
    {
        new("Ollama",      "local",  RequiredConfig.None),
        new("Claude",      "cloud",  RequiredConfig.ApiKey),
        new("OpenAI",      "cloud",  RequiredConfig.ApiKey),
        new("Groq",        "cloud",  RequiredConfig.ApiKey),
        new("OpenRouter",  "cloud",  RequiredConfig.ApiKey),
        new("LMStudio",    "local",  RequiredConfig.BaseUrl),
    };

    public AIProviderFactory(OllamaProvider ollama, IConfiguration config, ILoggerFactory loggerFactory)
    {
        _ollama = ollama;
        _config = config;
        _loggerFactory = loggerFactory;
        _current = ollama;
    }

    public IAIProvider GetCurrent() => _current;

    public IAIProvider GetProvider(string name) =>
        IsOllama(name) ? _ollama : Build(name);

    public void SetCurrent(string name, string? apiKey = null, string? baseUrl = null) =>
        _current = IsOllama(name) ? _ollama : Build(name, apiKey, baseUrl);

    public IEnumerable<string> GetAvailableProviders() =>
        Providers.Select(p => p.Name);

    public IEnumerable<ProviderInfo> GetConfiguredProviders() =>
        Providers.Select(p =>
        {
            var key = _config[$"AI:{p.Name}:ApiKey"] ?? "";
            var url = _config[$"AI:{p.Name}:BaseUrl"] ?? "";
            var hasConfig = p.Required switch
            {
                RequiredConfig.None => true,          // Ollama — URL zawsze skonfigurowany w appsettings (online/offline sprawdza IsAvailableAsync)
                RequiredConfig.ApiKey => !string.IsNullOrWhiteSpace(key),
                RequiredConfig.BaseUrl => !string.IsNullOrWhiteSpace(url),
                _ => false
            };
            return new ProviderInfo(p.Name, p.Name, p.Type, url, hasConfig);
        });

    private IAIProvider Build(string name, string? apiKey = null, string? baseUrl = null)
    {
        var key = apiKey ?? _config[$"AI:{name}:ApiKey"] ?? "";
        var url = baseUrl ?? _config[$"AI:{name}:BaseUrl"] ?? "";

        return name switch
        {
            "Claude" => new ClaudeProvider(new HttpClient(), _loggerFactory.CreateLogger<ClaudeProvider>(), key),
            "OpenAI" => new OpenAIProvider(new HttpClient(), _loggerFactory.CreateLogger<OpenAIProvider>(), key),
            "Groq" => new GroqProvider(new HttpClient(), _loggerFactory.CreateLogger<GroqProvider>(), key),
            "OpenRouter" => new OpenRouterProvider(new HttpClient(), _loggerFactory.CreateLogger<OpenRouterProvider>(), key),
            "LMStudio" => new LMStudioProvider(new HttpClient(), _loggerFactory.CreateLogger<LMStudioProvider>(), url),
            _ => throw new ArgumentException($"Unknown provider: {name}")
        };
    }

    private static bool IsOllama(string name) =>
        name.Equals("Ollama", StringComparison.OrdinalIgnoreCase);

    private record ProviderMeta(string Name, string Type, RequiredConfig Required);
}

public enum RequiredConfig { None, ApiKey, BaseUrl }