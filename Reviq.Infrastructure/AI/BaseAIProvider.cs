using System.Text;
using Reviq.Application.Interfaces;
using Reviq.Domain.Enums;

namespace Reviq.Infrastructure.AI;

public abstract class BaseAIProvider : IAIProvider
{
    public abstract ProviderName Name { get; }
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

        // Ponumeruj linie żeby model wiedział o których mówi
        var numberedCode = AddLineNumbers(TruncateCode(code));

        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert {language} code reviewer.");
        sb.AppendLine($"IMPORTANT: Analyze ONLY the exact code provided below. Do NOT use examples from your training data. Do NOT invent code that is not present.");
        sb.AppendLine($"File: {filePath}");
        sb.AppendLine($"Language: {language}");
        sb.AppendLine($"Categories to check: {categoryList}. Ignore all other categories.");
        sb.AppendLine();
        sb.AppendLine($"=== CODE TO REVIEW (lines are numbered) ===");
        sb.AppendLine(numberedCode);
        sb.AppendLine($"=== END OF CODE ===");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Report ONLY issues you can see in the code above");
        sb.AppendLine("- The \"line\" field MUST match the line number shown in the code above");
        sb.AppendLine("- codeBefore MUST be the exact lines from the code above (copy them as-is, without line number prefix)");
        sb.AppendLine("- codeAfter MUST be only those same lines rewritten to fix the issue");
        sb.AppendLine("- Do NOT report issues about XML/doc comments — they are always correct style for the given language");
        sb.AppendLine("- Do NOT report issues about comment formatting or style");
        sb.AppendLine("- Do NOT report issues about modern language features or syntax — assume the developer intentionally uses the latest language version");
        sb.AppendLine("- Return valid JSON ONLY. No markdown. No explanation outside JSON.");
        sb.AppendLine();
        sb.AppendLine("JSON structure:");
        sb.AppendLine("{");
        sb.AppendLine("  \"score\": <0-100 integer>,");
        sb.AppendLine("  \"issues\": [");
        sb.AppendLine("    {");
        sb.AppendLine($"      \"severity\": \"<Critical|Warning|Info>\",");
        sb.AppendLine($"      \"category\": \"<{string.Join("|", allowed)}>\",");
        sb.AppendLine("      \"line\": <integer line number from the numbered code>,");
        sb.AppendLine("      \"title\": \"<short title>\",");
        sb.AppendLine("      \"description\": \"<what exactly is wrong in this file>\",");
        sb.AppendLine("      \"suggestion\": \"<concrete fix>\",");
        sb.AppendLine("      \"codeBefore\": \"<exact lines from the code above that have the issue>\",");
        sb.AppendLine("      \"codeAfter\": \"<those exact lines with the fix applied — MUST be actual code, NOT a description or suggestion — if you cannot write the fixed code, omit this field>\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"summary\": \"<2-3 sentence summary of this specific file>\"");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string TruncateCode(string code, int maxLines = 500)
    {
        var lines = code.Split('\n');
        if (lines.Length <= maxLines) return code;
        var truncated = lines.Take(maxLines);
        return string.Join('\n', truncated) +
               $"\n\n// ... [truncated: {lines.Length - maxLines} more lines not shown]";
    }

    private static string AddLineNumbers(string code)
    {
        var lines = code.Split('\n');
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
            sb.AppendLine($"{i + 1,4} | {lines[i].TrimEnd()}");
        return sb.ToString();
    }
}