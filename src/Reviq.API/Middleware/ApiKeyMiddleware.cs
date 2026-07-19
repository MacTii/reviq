using Microsoft.Extensions.Options;
using Reviq.API.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Reviq.API.Middleware;

// Optional, off-by-default gate for the API surface. Disabled while Security:ApiKey is empty
// (the default), matching how AI provider keys work elsewhere in this app. Webhook endpoints
// are excluded — they authenticate via their own signature/token instead (see WebhookController).
public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context, IOptions<SecurityOptions> options)
    {
        var apiKey = options.Value.ApiKey;

        if (string.IsNullOrEmpty(apiKey) ||
            !context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Path.StartsWithSegments("/api/webhook"))
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next(context);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
