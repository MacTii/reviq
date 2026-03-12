using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Reviq.Infrastructure.AI;

public class ClaudeProvider : BaseAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeProvider> _logger;

    public override string ProviderName => "Claude";

    private static readonly string[] Models =
        { "claude-haiku-4-5", "claude-sonnet-4-5", "claude-opus-4-5" };

    public ClaudeProvider(HttpClient httpClient, ILogger<ClaudeProvider> logger, string apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;
        CurrentModel = "claude-haiku-4-5";

        _httpClient.BaseAddress = new Uri("https://api.anthropic.com");
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public override void SetModel(string model) => CurrentModel = model;

    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            var test = new
            {
                model = "claude-haiku-4-5",
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "hi" } }
            };
            var r = await _httpClient.PostAsJsonAsync("/v1/messages", test);
            return r.IsSuccessStatusCode || r.StatusCode == System.Net.HttpStatusCode.BadRequest;
        }
        catch { return false; }
    }

    public override Task<List<string>> GetAvailableModelsAsync() =>
        Task.FromResult(Models.ToList());

    public override async Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null)
    {
        var request = new
        {
            model = CurrentModel,
            max_tokens = 4000,
            system = "You are an expert code reviewer. Always respond with valid JSON only, no markdown.",
            messages = new[] { new { role = "user", content = BuildPrompt(code, language, filePath, categories) } }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/v1/messages", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Claude] ReviewCode failed for {FilePath}", filePath);
            throw;
        }
    }
}