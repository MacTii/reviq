using Microsoft.EntityFrameworkCore;
using Reviq.API.Middleware;
using Reviq.API.Webhooks;
using Reviq.Application;
using Reviq.Infrastructure;
using Reviq.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5000"];

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IWebhookQueue, WebhookQueue>();
builder.Services.AddHostedService<WebhookProcessingService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ReviqDbContext>().Database.Migrate();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseStaticFiles();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

app.Run();