using Reviq.Infrastructure.AI;
using Microsoft.Extensions.Options;
using Reviq.Infrastructure.Configuration;
using Reviq.Domain.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Reviq.Infrastructure.AI.Providers;

public sealed class OllamaProvider : BaseAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaProvider> _logger;

    public override ProviderName Name => ProviderName.Ollama;

    public OllamaProvider(HttpClient httpClient, ILogger<OllamaProvider> logger, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        CurrentModel = options.Value.DefaultModel;
    }


    public override async Task<bool> IsAvailableAsync()
    {
        try { var r = await _httpClient.GetAsync("/api/tags"); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public override async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var r = await _httpClient.GetAsync("/api/tags");
            if (!r.IsSuccessStatusCode) return new();
            var json = await r.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
        }
        catch { return new(); }
    }

    public override async Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null)
    {
        var request = new
        {
            model = CurrentModel,
            prompt = BuildPrompt(code, language, filePath, categories),
            stream = false,
            options = new
            {
                temperature = 0.05f,
                top_p = 0.85f,
                top_k = 20,
                repeat_penalty = 1.15f,
                num_predict = 6000,
                num_ctx = 8192,
                num_thread = Math.Max(1, Environment.ProcessorCount - 2),
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Response ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Ollama] ReviewCode failed for {FilePath}", filePath);
            throw;
        }
    }

    private sealed class OllamaResponse
    {
        public string Response { get; set; } = "";
        public bool Done { get; set; }
    }
}