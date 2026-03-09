using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;
using System.Diagnostics;
using System.Text;

namespace Reviq.Infrastructure.Git;

public class GitService : IGitProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".go",
        ".cpp", ".c", ".h", ".rs", ".php", ".rb", ".swift", ".kt"
    };

    public async Task<RepoInfo> GetRepoInfoAsync(string repoPath, string? commitHash = null)
    {
        if (!Directory.Exists(repoPath) || !Directory.Exists(Path.Combine(repoPath, ".git")))
            return new RepoInfo { Error = "Podana ścieżka nie jest repozytorium Git." };

        var branch = await RunGitAsync(repoPath, "rev-parse --abbrev-ref HEAD");
        var latestCommit = await RunGitAsync(repoPath, "rev-parse --short HEAD");
        var commitMessage = await RunGitAsync(repoPath, "log -1 --pretty=%s");

        var diffOutput = string.IsNullOrWhiteSpace(commitHash)
            ? await RunGitAsync(repoPath, "diff --name-only HEAD~1 HEAD")
            : await RunGitAsync(repoPath, $"diff --name-only {commitHash}~1 {commitHash}");

        if (string.IsNullOrWhiteSpace(diffOutput))
            diffOutput = await RunGitAsync(repoPath, "ls-files");

        var changedFiles = diffOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .Take(20)
            .ToList();

        return new RepoInfo
        {
            IsValid = true,
            Branch = branch,
            LatestCommit = latestCommit,
            CommitMessage = commitMessage,
            ChangedFiles = changedFiles
        };
    }

    public async Task<Dictionary<string, string>> GetFileContentsAsync(string repoPath, List<string> files)
    {
        var contents = new Dictionary<string, string>();

        foreach (var file in files)
        {
            var fullPath = Path.Combine(repoPath, file);
            if (!File.Exists(fullPath)) continue;
            if (!SupportedExtensions.Contains(Path.GetExtension(file))) continue;

            var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
            if (content.Length > 15_000)
                content = content[..15_000] + "\n// [TRUNCATED]";

            contents[file] = content;
        }

        return contents;
    }

    public string DetectLanguage(string filePath) => Path.GetExtension(filePath).ToLower() switch
    {
        ".cs" => "C#",
        ".ts" or ".tsx" => "TypeScript",
        ".js" or ".jsx" => "JavaScript",
        ".py" => "Python",
        ".java" => "Java",
        ".go" => "Go",
        ".cpp" or ".c" or ".h" => "C/C++",
        ".rs" => "Rust",
        ".php" => "PHP",
        ".rb" => "Ruby",
        ".swift" => "Swift",
        ".kt" => "Kotlin",
        _ => "Unknown"
    };

    private static async Task<string> RunGitAsync(string repoPath, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
