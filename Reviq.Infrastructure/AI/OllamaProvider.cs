using Reviq.Domain.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Reviq.Infrastructure.AI;

public class OllamaProvider : BaseAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaProvider> _logger;

    public override ProviderName Name => ProviderName.Ollama;

    public OllamaProvider(HttpClient httpClient, ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        CurrentModel = "deepseek-coder-v2";
    }

    public override void SetModel(string model) => CurrentModel = model;

    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            var r = await _httpClient.GetAsync("/api/tags");
            return r.IsSuccessStatusCode;
        }
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
            options = new { temperature = 0.1f, num_predict = 16000, num_ctx = 8192 }
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