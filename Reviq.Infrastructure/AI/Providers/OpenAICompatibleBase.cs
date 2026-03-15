using Reviq.Infrastructure.AI;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Reviq.Domain.Enums;
namespace Reviq.Infrastructure.AI.Providers;

/// <summary>
/// Baza dla providerów używających OpenAI-compatible API (/v1/chat/completions).
/// Dziedziczy: OpenAIProvider, GroqProvider, OpenRouterProvider, LMStudioProvider.
/// </summary>
public abstract class OpenAICompatibleBase : BaseAIProvider
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    protected OpenAICompatibleBase(HttpClient httpClient, ILogger logger, string baseUrl, string apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(3);

        if (!string.IsNullOrWhiteSpace(apiKey))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
    }


    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            var r = await _httpClient.GetAsync("/models");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public override async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var r = await _httpClient.GetAsync("/models");
            if (!r.IsSuccessStatusCode) return new();

            var json = await r.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("data")
                .EnumerateArray()
                .Select(m => m.GetProperty("id").GetString() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();
        }
        catch { return new(); }
    }

    public override async Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null)
    {
        var request = new
        {
            model = CurrentModel,
            messages = new[]
            {
                new { role = "system", content = "You are an expert code reviewer. Always respond with valid JSON only, no markdown." },
                new { role = "user",   content = BuildPrompt(code, language, filePath, categories) }
            },
            temperature = 0.1,
            max_tokens = 16000
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/chat/completions", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Provider}] ReviewCode failed for {FilePath}", Name, filePath);
            throw;
        }
    }
}