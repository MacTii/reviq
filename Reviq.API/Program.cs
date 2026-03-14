using Microsoft.Extensions.Logging;
using Reviq.API.Middleware;
using Reviq.Application.Interfaces;
using Reviq.Application.UseCases.GetRepoInfo;
using Reviq.Application.UseCases.HandleWebhook;
using Reviq.Application.UseCases.RunReview;
using Reviq.Domain.Interfaces;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.Git;
using Reviq.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── MVC + Swagger ─────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── HTTP Clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient(); // rejestruje IHttpClientFactory
builder.Services.AddHttpClient<OllamaProvider>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(15);
});

builder.Services.AddHttpClient<GitHubProvider>();
builder.Services.AddHttpClient<GitLabProvider>();

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IGitProvider, GitService>();
builder.Services.AddSingleton<IReviewRepository, ReviewRepository>();

// ── AI Provider ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<AIProviderFactory>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var ollamaClient = httpClientFactory.CreateClient("OllamaFactory");
    ollamaClient.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    ollamaClient.Timeout = TimeSpan.FromMinutes(15);

    var config = sp.GetRequiredService<IConfiguration>();
    var logFact = sp.GetRequiredService<ILoggerFactory>();

    var modelsDir = config["LocalAI:ModelsDir"]
                    ?? Path.Combine(AppContext.BaseDirectory, "Models");

    var ollama = new OllamaProvider(ollamaClient, logFact.CreateLogger<OllamaProvider>());
    var localAI = new LocalAIProvider(logFact.CreateLogger<LocalAIProvider>(), modelsDir);

    return new AIProviderFactory(ollama, localAI, config, logFact);
});
builder.Services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderFactory>());
builder.Services.AddSingleton<IAIProvider>(sp => sp.GetRequiredService<AIProviderFactory>().GetCurrent());

// ── Git Host Providers ────────────────────────────────────────────────────────
builder.Services.AddScoped<IGitHostProviderFactory, GitHostProviderFactory>();

// ── Application Use Cases ─────────────────────────────────────────────────────
builder.Services.AddScoped<RunReviewHandler>();
builder.Services.AddScoped<GetRepoInfoHandler>();
builder.Services.AddScoped<HandleWebhookHandler>();

// ── Pipeline ──────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run("http://localhost:5000");