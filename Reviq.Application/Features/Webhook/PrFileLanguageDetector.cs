namespace Reviq.Application.Features.Webhook;

public static class PrFileLanguageDetector
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionToLanguage =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "C#",
            [".ts"] = "TypeScript",
            [".tsx"] = "TypeScript",
            [".js"] = "JavaScript",
            [".jsx"] = "JavaScript",
            [".py"] = "Python",
            [".java"] = "Java",
            [".go"] = "Go",
            [".rs"] = "Rust",
            [".php"] = "PHP",
        };

    public static IReadOnlyCollection<string> SupportedExtensions => (IReadOnlyCollection<string>)ExtensionToLanguage.Keys;

    public static bool IsSupported(string fileName) => ExtensionToLanguage.ContainsKey(Path.GetExtension(fileName));

    public static string Detect(string fileName) =>
        ExtensionToLanguage.TryGetValue(Path.GetExtension(fileName), out var language) ? language : "Unknown";
}
