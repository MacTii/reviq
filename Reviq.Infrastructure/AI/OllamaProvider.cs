using Microsoft.Extensions.Logging;
using Reviq.Application.Interfaces;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Reviq.Infrastructure.AI;

public class OllamaProvider(HttpClient httpClient, ILogger<OllamaProvider> logger) : IAIProvider
{
    public string CurrentModel { get; private set; } = "deepseek-coder-v2";

    public void SetModel(string model) => CurrentModel = model;

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("/api/tags");
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    public async Task<string> ReviewCodeAsync(string code, string language, string filePath)
    {
        var request = new
        {
            model = CurrentModel,
            prompt = BuildPrompt(code, language, filePath),
            stream = false,
            options = new { temperature = 0.1f, num_predict = 3000 }
        };

        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/generate", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Response ?? "";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ollama request failed for {FilePath}", filePath);
            throw;
        }
    }

    private static string BuildPrompt(string code, string language, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert {language} code reviewer. Analyze the following code and return a JSON response ONLY — no markdown, no explanation outside JSON.");
        sb.AppendLine();
        sb.AppendLine($"File: {filePath}");
        sb.AppendLine($"Language: {language}");
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
        sb.AppendLine("      \"severity\": \"<Critical|Warning|Info>\",");
        sb.AppendLine("      \"category\": \"<Bug|Security|BestPractice|Refactor>\",");
        sb.AppendLine("      \"line\": <line number or null>,");
        sb.AppendLine("      \"title\": \"<short title>\",");
        sb.AppendLine("      \"description\": \"<detailed description>\",");
        sb.AppendLine("      \"suggestion\": \"<concrete fix suggestion>\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"summary\": \"<2-3 sentence overall feedback>\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("severity: Critical=bugs/security holes, Warning=bad practices, Info=style/minor");
        sb.AppendLine("category: Bug=logic errors, Security=injections/auth, BestPractice=SOLID/DRY, Refactor=smells/complexity");
        sb.AppendLine("Return valid JSON only. No markdown code blocks.");
        return sb.ToString();
    }

    private sealed class OllamaResponse
    {
        public string Response { get; set; } = "";
        public bool Done { get; set; }
    }
}
