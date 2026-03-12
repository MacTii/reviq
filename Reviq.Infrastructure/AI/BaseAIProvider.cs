using System.Text;
using Reviq.Application.Interfaces;

namespace Reviq.Infrastructure.AI;

/// <summary>
/// Wspólna logika dla wszystkich providerów AI — budowanie promptu i interfejs.
/// </summary>
public abstract class BaseAIProvider : IAIProvider
{
    public abstract string ProviderName { get; }
    public string CurrentModel { get; protected set; } = "";

    public abstract void SetModel(string model);
    public abstract Task<bool> IsAvailableAsync();
    public abstract Task<List<string>> GetAvailableModelsAsync();
    public abstract Task<string> ReviewCodeAsync(string code, string language, string filePath, IList<string>? categories = null);

    protected static string BuildPrompt(string code, string language, string filePath, IList<string>? categories)
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