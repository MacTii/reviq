using Microsoft.EntityFrameworkCore;
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
            .AddPersistence(configuration)
            .AddRepositories()
            .AddAIProviders()
            .AddLocalAI();

        return services;
    }

    // ── PERSISTENCE ───────────────────────────────────────────────────────────

    private static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=reviq.db";

        services.AddDbContext<ReviqDbContext>(opts => opts.UseSqlite(connectionString));
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
        services.AddHttpClient(nameof(HttpPrFileContentFetcher));

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
        services.AddScoped<IReviewRepository, SqliteReviewRepository>();
        services.AddSingleton<IGitProvider, GitService>();
        services.AddScoped<IGitHostProviderFactory, GitHostProviderFactory>();
        services.AddScoped<IPrFileContentFetcher, HttpPrFileContentFetcher>();
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

        // Cloud providers — each registered via the shared factory helper to avoid
        // repeating the "resolve HttpClient + logger + options" boilerplate per provider.
        services.AddCloudProvider<ClaudeProvider>((http, log, opts) => new ClaudeProvider(http, log, opts.Claude));
        services.AddCloudProvider<OpenAIProvider>((http, log, opts) => new OpenAIProvider(http, log, opts.OpenAI));
        services.AddCloudProvider<GroqProvider>((http, log, opts) => new GroqProvider(http, log, opts.Groq));
        services.AddCloudProvider<OpenRouterProvider>((http, log, opts) => new OpenRouterProvider(http, log, opts.OpenRouter));
        services.AddCloudProvider<LMStudioProvider>((http, log, opts) => new LMStudioProvider(http, log, opts.LMStudio));

        // Factory
        services.AddSingleton<AIProviderFactory>(sp => new AIProviderFactory(
            sp.GetRequiredService<IEnumerable<IAIProvider>>(),
            sp.GetRequiredService<IOptions<AIProviderOptions>>().Value));

        services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderFactory>());

        return services;
    }

    private static IServiceCollection AddCloudProvider<T>(
        this IServiceCollection services, Func<HttpClient, ILogger<T>, AIProviderOptions, T> factory)
        where T : class, IAIProvider
    {
        services.AddSingleton<IAIProvider>(sp => factory(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(T).Name),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<T>(),
            sp.GetRequiredService<IOptions<AIProviderOptions>>().Value));
        return services;
    }

    // ── LOCAL AI ───────────────────────────────────────────────────────────────

    private static IServiceCollection AddLocalAI(this IServiceCollection services)
    {
        services.AddSingleton<HuggingFaceClient>();
        services.AddSingleton<ILocalAIService, LocalAIService>();
        return services;
    }
}