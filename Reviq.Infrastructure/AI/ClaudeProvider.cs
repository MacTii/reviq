using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;

namespace Reviq.Infrastructure.AI;

public class ClaudeProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeProvider> _logger;

    public string ProviderName => "claude";
    public string CurrentModel { get; private set; } = "claude-haiku-4-5";

    private static readonly string[] AvailableModels =
    {
        "claude-haiku-4-5",
        "claude-sonnet-4-5",
        "claude-opus-4-5"
    };

    public ClaudeProvider(HttpClient httpClient, ILogger<ClaudeProvider> logger, string apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.anthropic.com");
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public void SetModel(string model) => CurrentModel = model;

    public async Task<bool> IsAvailableAsync()
    {
        // Claude API nie ma endpointu /models bez klucza — sprawdzamy przez mini-request
        try
        {
            var testReq = new
            {
                model = "claude-haiku-4-5",
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "hi" } }
            };
            var r = await _httpClient.PostAsJsonAsync("/v1/messages", testReq);
            // 200 lub 400 (bad request) = API żyje i klucz jest OK
            // 401 = zły klucz
            return r.IsSuccessStatusCode || r.StatusCode == System.Net.HttpStatusCode.BadRequest;
        }
        catch { return false; }
    }

    public Task<List<string>> GetAvailableModelsAsync() =>
        Task.FromResult(AvailableModels.ToList());

    public async Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null)
    {
        var prompt = BuildPrompt(code, language, filePath, categories);

        var request = new
        {
            model = CurrentModel,
            max_tokens = 4000,
            system = "You are an expert code reviewer. Always respond with valid JSON only, no markdown, no explanation outside JSON.",
            messages = new[] { new { role = "user", content = prompt } }
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
            _logger.LogError(ex, "Claude request failed [{Model}] {FilePath}", CurrentModel, filePath);
            throw;
        }
    }

    private static string BuildPrompt(string code, string language, string filePath, IList<string>? categories)
    {
        var allowed = (categories == null || categories.Count == 0)
            ? new[] { "Bug", "Security", "BestPractice", "Refactor" }
            : categories.ToArray();
        var categoryList = string.Join(", ", allowed);

        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert {language} code reviewer. Analyze the following code and return a JSON response ONLY — no markdown, no explanation outside JSON.");
        sb.AppendLine($"File: {filePath}");
        sb.AppendLine($"Language: {language}");
        sb.AppendLine($"Focus ONLY on these issue categories: {categoryList}. Do NOT report issues from other categories.");
        sb.AppendLine();
        sb.AppendLine("Code:");
        sb.AppendLine("```");
        sb.AppendLine(code);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Return this exact JSON structure:");
        sb.AppendLine("{");
        sb.AppendLine("  \"score\": <0-100 integer>,");
        sb.AppendLine("  \"issues\": [");
        sb.AppendLine("    {");
        sb.AppendLine($"      \"severity\": \"<Critical|Warning|Info>\",");
        sb.AppendLine($"      \"category\": \"<{string.Join("|", allowed)}>\",");
        sb.AppendLine("      \"line\": <line number or null>,");
        sb.AppendLine("      \"title\": \"<short title>\",");
        sb.AppendLine("      \"description\": \"<detailed description>\",");
        sb.AppendLine("      \"suggestion\": \"<concrete fix suggestion>\",");
        sb.AppendLine("      \"codeBefore\": \"<ONLY the specific problematic lines, 3-8 lines max>\",");
        sb.AppendLine("      \"codeAfter\": \"<ONLY those same lines rewritten to fix the issue>\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"summary\": \"<2-3 sentence overall feedback>\"");
        sb.AppendLine("}");
        sb.AppendLine("severity: Critical=bugs/security holes, Warning=bad practices, Info=style/minor");
        sb.AppendLine($"category: only report from this list: {categoryList}");
        sb.AppendLine("Return valid JSON only. No markdown code blocks.");
        return sb.ToString();
    }
}