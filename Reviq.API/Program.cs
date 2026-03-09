using Reviq.API.Middleware;
using Reviq.Application.Interfaces;
using Reviq.Application.UseCases.GetRepoInfo;
using Reviq.Application.UseCases.RunReview;
using Reviq.Domain.Interfaces;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.Git;
using Reviq.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Infrastructure
builder.Services.AddHttpClient<OllamaProvider>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<IGitProvider, GitService>();
builder.Services.AddSingleton<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IAIProvider>(sp => sp.GetRequiredService<OllamaProvider>());

// Application
builder.Services.AddScoped<RunReviewHandler>();
builder.Services.AddScoped<GetRepoInfoHandler>();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapOpenApi();
app.UseCors();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run("http://localhost:5000");