using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;

namespace Reviq.Infrastructure.AI;

/// <summary>
/// Provider dla OpenAI-compatible APIs: OpenAI, Groq, OpenRouter, LM Studio, itp.
/// </summary>
public class OpenAICompatibleProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAICompatibleProvider> _logger;

    public string ProviderName { get; private set; }
    public string CurrentModel { get; private set; }

    // Predefiniowane konfiguracje providerów
    public static readonly Dictionary<string, (string BaseUrl, string[] DefaultModels)> KnownProviders = new()
    {
        ["openai"] = ("https://api.openai.com/v1", new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo" }),
        ["groq"] = ("https://api.groq.com/openai/v1", new[] { "llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it" }),
        ["openrouter"] = ("https://openrouter.ai/api/v1", new[] { "anthropic/claude-3.5-sonnet", "google/gemini-flash-1.5", "meta-llama/llama-3.1-8b-instruct:free" }),
        ["lmstudio"] = ("http://localhost:1234/v1", new[] { "local-model" }),
    };

    public OpenAICompatibleProvider(HttpClient httpClient, ILogger<OpenAICompatibleProvider> logger,
        string providerName = "openai", string? baseUrl = null, string? apiKey = null, string? defaultModel = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        ProviderName = providerName;

        var resolvedUrl = baseUrl
            ?? (KnownProviders.TryGetValue(providerName, out var cfg) ? cfg.BaseUrl : "https://api.openai.com/v1");

        _httpClient.BaseAddress = new Uri(resolvedUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(3);

        if (!string.IsNullOrWhiteSpace(apiKey))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

        // OpenRouter wymaga extra headera
        if (providerName == "openrouter")
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", "https://reviq.app");

        CurrentModel = defaultModel
            ?? (KnownProviders.TryGetValue(providerName, out var c) ? c.DefaultModels[0] : "gpt-4o-mini");
    }

    public void SetModel(string model) => CurrentModel = model;

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var r = await _httpClient.GetAsync("/models");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        // Dla znanych providerów zwróć predefiniowaną listę — szybciej i nie wymaga klucza
        if (KnownProviders.TryGetValue(ProviderName, out var cfg))
            return cfg.DefaultModels.ToList();

        // Dla custom URL — spróbuj pobrać z /models
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

    public async Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null)
    {
        var prompt = BuildPrompt(code, language, filePath, categories);

        var request = new
        {
            model = CurrentModel,
            messages = new[]
            {
                new { role = "system", content = "You are an expert code reviewer. Always respond with valid JSON only, no markdown." },
                new { role = "user",   content = prompt }
            },
            temperature = 0.1,
            max_tokens = 4000
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
            _logger.LogError(ex, "OpenAI-compatible request failed [{Provider}] {FilePath}", ProviderName, filePath);
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