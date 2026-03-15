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

    private static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHttpClient<OllamaProvider>();
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

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IReviewRepository, ReviewRepository>();
        services.AddSingleton<IGitProvider, GitService>();
        services.AddScoped<IGitHostProviderFactory, GitHostProviderFactory>();
        return services;
    }

    private static IServiceCollection AddAIProviders(this IServiceCollection services)
    {
        // Każdy provider rejestrowany jako IAIProvider - DI zbiera je w IEnumerable<IAIProvider>
        services.AddSingleton<IAIProvider, LocalAIProvider>();
        services.AddSingleton<IAIProvider, OllamaProvider>();
        services.AddSingleton<IAIProvider>(sp => {
            var (http, log, opts) = CloudProviderDeps<ClaudeProvider>(sp);
            return new ClaudeProvider(http, log, opts.Claude);
        });
        services.AddSingleton<IAIProvider>(sp => {
            var (http, log, opts) = CloudProviderDeps<OpenAIProvider>(sp);
            return new OpenAIProvider(http, log, opts.OpenAI);
        });
        services.AddSingleton<IAIProvider>(sp => {
            var (http, log, opts) = CloudProviderDeps<GroqProvider>(sp);
            return new GroqProvider(http, log, opts.Groq);
        });
        services.AddSingleton<IAIProvider>(sp => {
            var (http, log, opts) = CloudProviderDeps<OpenRouterProvider>(sp);
            return new OpenRouterProvider(http, log, opts.OpenRouter);
        });
        services.AddSingleton<IAIProvider>(sp => {
            var (http, log, opts) = CloudProviderDeps<LMStudioProvider>(sp);
            return new LMStudioProvider(http, log, opts.LMStudio);
        });

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

    private static IServiceCollection AddLocalAI(this IServiceCollection services)
    {
        services.AddSingleton<HuggingFaceClient>();
        services.AddSingleton<ILocalAIService, LocalAIService>();
        return services;
    }
}