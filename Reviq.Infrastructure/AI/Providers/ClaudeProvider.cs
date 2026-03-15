using Microsoft.Extensions.Logging;
using Reviq.Domain.Enums;
using Reviq.Infrastructure.AI;
using Reviq.Infrastructure.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace Reviq.Infrastructure.AI.Providers;

public sealed class ClaudeProvider : BaseAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeProvider> _logger;

    public override ProviderName Name => ProviderName.Claude;

    public ClaudeProvider(HttpClient httpClient, ILogger<ClaudeProvider> logger, ProviderConfig config)
    {
        _httpClient = httpClient;
        _logger = logger;
        CurrentModel = config.DefaultModel;

        _httpClient.BaseAddress = new Uri(config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", config.AnthropicVersion);
    }

    public override async Task<bool> IsAvailableAsync()
    {
        try { var r = await _httpClient.GetAsync("/v1/models"); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public override Task<List<string>> GetAvailableModelsAsync() =>
        Task.FromResult(new List<string> { "claude-haiku-4-5", "claude-sonnet-4-5", "claude-opus-4-5" });

    public override async Task<string> ReviewCodeAsync(
        string code, string language, string filePath, IList<string>? categories = null)
    {
        var request = new
        {
            model = CurrentModel,
            max_tokens = 16000,
            messages = new[] { new { role = "user", content = BuildPrompt(code, language, filePath, categories) } }
        };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/v1/messages", request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Claude] ReviewCode failed for {FilePath}", filePath);
            throw;
        }
    }
}