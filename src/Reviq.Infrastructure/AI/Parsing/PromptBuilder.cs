using System.Text;

namespace Reviq.Infrastructure.AI.Parsing;

public static class PromptBuilder
{
    private const int MaxLines = 500;

    public static string Build(string code, string language, string filePath, IList<string>? categories)
    {
        var allowed = categories?.Count > 0 ? categories : new[] { "Bug", "Security", "BestPractice", "Refactor" };
        var categoryList = string.Join(", ", allowed);
        var numberedCode = AddLineNumbers(Truncate(code));

        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert {language} code reviewer.");
        sb.AppendLine("IMPORTANT: Analyze ONLY the exact code provided below. Do NOT use examples from training data.");
        sb.AppendLine($"File: {filePath}");
        sb.AppendLine($"Language: {language}");
        sb.AppendLine($"Categories to check: {categoryList}. Ignore all other categories.");
        sb.AppendLine();
        sb.AppendLine("=== CODE TO REVIEW (lines are numbered) ===");
        sb.AppendLine(numberedCode);
        sb.AppendLine("=== END OF CODE ===");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Report ONLY issues visible in the code above");
        sb.AppendLine("- The \"line\" field MUST match the line number shown above");
        sb.AppendLine("- codeBefore MUST be exact lines from the code (copy as-is, without line number prefix)");
        sb.AppendLine("- codeAfter MUST be those same lines rewritten to fix the issue");
        sb.AppendLine("- Do NOT report issues about XML/doc comments, comment formatting or modern language syntax");
        sb.AppendLine("- Return valid JSON ONLY. No markdown. No explanation outside JSON.");
        sb.AppendLine();
        sb.AppendLine("JSON structure:");
        sb.AppendLine("{");
        sb.AppendLine("  \"score\": <0-100 integer>,");
        sb.AppendLine("  \"issues\": [");
        sb.AppendLine("    {");
        sb.AppendLine($"      \"severity\": \"<Critical|Warning|Info>\",");
        sb.AppendLine($"      \"category\": \"<{string.Join("|", allowed)}>\",");
        sb.AppendLine("      \"line\": <integer>,");
        sb.AppendLine("      \"title\": \"<short title>\",");
        sb.AppendLine("      \"description\": \"<what is wrong>\",");
        sb.AppendLine("      \"suggestion\": \"<concrete fix>\",");
        sb.AppendLine("      \"codeBefore\": \"<exact lines with issue>\",");
        sb.AppendLine("      \"codeAfter\": \"<fixed lines — MUST be actual code, NOT description>\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"summary\": \"<2-3 sentence summary>\"");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Truncate(string code)
    {
        var lines = code.Split('\n');
        if (lines.Length <= MaxLines) return code;
        return string.Join('\n', lines.Take(MaxLines))
               + $"\n\n// ... [truncated: {lines.Length - MaxLines} more lines]";
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
