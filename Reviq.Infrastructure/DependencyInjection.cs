using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reviq.Application.Interfaces;
using Reviq.Domain.Interfaces;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.AI.Providers;
using Reviq.Infrastructure.Configuration;
using Reviq.Infrastructure.Git;
using Reviq.Infrastructure.LocalAI.HuggingFace;
using Reviq.Infrastructure.LocalAI.Services;
using Reviq.Infrastructure.Persistence;

namespace Reviq.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions(configuration)
            .AddHttpClients()
            .AddRepositories()
            .AddAIProviders()
            .AddLocalAI();

        return services;
    }

    // ── OPTIONS ────────────────────────────────────────────────────────────────

    private static IServiceCollection AddOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.Section));
        services.Configure<HuggingFaceOptions>(configuration.GetSection(HuggingFaceOptions.Section));
        services.Configure<LocalAIOptions>(configuration.GetSection(LocalAIOptions.Section));
        services.Configure<AIProviderOptions>(configuration.GetSection(AIProviderOptions.Section));
        services.Configure<GitOptions>(configuration.GetSection(GitOptions.Section));
        return services;
    }

    // ── HTTP CLIENTS ───────────────────────────────────────────────────────────

    private static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient();

        // ✅ Konfiguracja dla Ollama (BaseAddress!)
        services.AddHttpClient(nameof(OllamaProvider), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        });

        services.AddHttpClient<GitHubProvider>();
        services.AddHttpClient<GitLabProvider>();

        services.AddHttpClient("HuggingFace", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<HuggingFaceOptions>>().Value;
            client.DefaultRequestHeaders.Add("User-Agent", opts.UserAgent);
            client.Timeout = TimeSpan.FromSeconds(opts.SearchTimeoutSeconds);
        });

        services.AddHttpClient("HuggingFaceDownload", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<HuggingFaceOptions>>().Value;
            client.DefaultRequestHeaders.Add("User-Agent", opts.UserAgent);
            client.Timeout = TimeSpan.FromHours(opts.DownloadTimeoutHours);
        });

        return services;
    }

    // ── REPOSITORIES ───────────────────────────────────────────────────────────

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IReviewRepository, ReviewRepository>();
        services.AddSingleton<IGitProvider, GitService>();
        services.AddScoped<IGitHostProviderFactory, GitHostProviderFactory>();
        return services;
    }

    // ── AI PROVIDERS ───────────────────────────────────────────────────────────

    private static IServiceCollection AddAIProviders(this IServiceCollection services)
    {
        // LocalAI
        services.AddSingleton<IAIProvider, LocalAIProvider>();

        // ✅ FIX: Ollama przez HttpClientFactory (żeby BaseAddress działał)
        services.AddSingleton<IAIProvider>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>()
                         .CreateClient(nameof(OllamaProvider));

            var logger = sp.GetRequiredService<ILogger<OllamaProvider>>();
            var options = sp.GetRequiredService<IOptions<OllamaOptions>>();

            return new OllamaProvider(http, logger, options);
        });

        // Cloud providers
        services.AddSingleton<IAIProvider>(sp =>
        {
            var (http, log, opts) = CloudProviderDeps<ClaudeProvider>(sp);
            return new ClaudeProvider(http, log, opts.Claude);
        });

        services.AddSingleton<IAIProvider>(sp =>
        {
            var (http, log, opts) = CloudProviderDeps<OpenAIProvider>(sp);
            return new OpenAIProvider(http, log, opts.OpenAI);
        });

        services.AddSingleton<IAIProvider>(sp =>
        {
            var (http, log, opts) = CloudProviderDeps<GroqProvider>(sp);
            return new GroqProvider(http, log, opts.Groq);
        });

        services.AddSingleton<IAIProvider>(sp =>
        {
            var (http, log, opts) = CloudProviderDeps<OpenRouterProvider>(sp);
            return new OpenRouterProvider(http, log, opts.OpenRouter);
        });

        services.AddSingleton<IAIProvider>(sp =>
        {
            var (http, log, opts) = CloudProviderDeps<LMStudioProvider>(sp);
            return new LMStudioProvider(http, log, opts.LMStudio);
        });

        // Factory
        services.AddSingleton<AIProviderFactory>(sp => new AIProviderFactory(
            sp.GetRequiredService<IEnumerable<IAIProvider>>(),
            sp.GetRequiredService<IOptions<AIProviderOptions>>().Value));

        services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderFactory>());

        return services;
    }

    private static (HttpClient http, ILogger<T> log, AIProviderOptions opts) CloudProviderDeps<T>(
        IServiceProvider sp) where T : class
        => (
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(T).Name),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<T>(),
            sp.GetRequiredService<IOptions<AIProviderOptions>>().Value
        );

    // ── LOCAL AI ───────────────────────────────────────────────────────────────

    private static IServiceCollection AddLocalAI(this IServiceCollection services)
    {
        services.AddSingleton<HuggingFaceClient>();
        services.AddSingleton<ILocalAIService, LocalAIService>();
        return services;
    }
}