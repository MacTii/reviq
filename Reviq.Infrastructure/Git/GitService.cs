using Reviq.Domain.Entities;
using Reviq.Domain.Enums;
using Reviq.Domain.Interfaces;
using System.Diagnostics;
using System.Text;

namespace Reviq.Infrastructure.Git;

public class GitService : IGitProvider
{
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".obj", ".bin", ".dat", ".db", ".sqlite",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp",
        ".zip", ".tar", ".gz", ".rar", ".7z",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".mp3", ".mp4", ".avi", ".mov", ".wav",
        ".ttf", ".woff", ".woff2", ".eot",
        ".lock"
    };

    public async Task<RepoInfo> GetRepoInfoAsync(string repoPath, DiffScope scope = DiffScope.LastCommit, string? commitHash = null)
    {
        if (!Directory.Exists(repoPath) || !Directory.Exists(Path.Combine(repoPath, ".git")))
            return new RepoInfo { Error = "Podana ścieżka nie jest repozytorium Git." };

        var branch = await RunGitAsync(repoPath, "rev-parse --abbrev-ref HEAD");
        var latestCommit = await RunGitAsync(repoPath, "rev-parse --short HEAD");
        var commitMessage = await RunGitAsync(repoPath, "log -1 --pretty=%s");

        string diffOutput;

        if (!string.IsNullOrWhiteSpace(commitHash))
        {
            diffOutput = await RunGitAsync(repoPath, $"diff --name-only {commitHash}~1 {commitHash}");
        }
        else
        {
            diffOutput = scope switch
            {
                DiffScope.LastCommit => await RunGitAsync(repoPath, "diff --name-only HEAD~1 HEAD"),
                DiffScope.SinceLastPush => await GetSinceLastPushAsync(repoPath, branch),
                DiffScope.Uncommitted => await GetUncommittedAsync(repoPath),
                DiffScope.AllFiles => await RunGitAsync(repoPath, "ls-files"),
                _ => await RunGitAsync(repoPath, "diff --name-only HEAD~1 HEAD")
            };
        }

        if (string.IsNullOrWhiteSpace(diffOutput))
            diffOutput = await RunGitAsync(repoPath, "ls-files");

        var changedFiles = diffOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => !string.IsNullOrEmpty(f) && !BinaryExtensions.Contains(Path.GetExtension(f)))
            .Distinct()
            .Take(50)
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

    private async Task<string> GetSinceLastPushAsync(string repoPath, string branch)
    {
        var remote = await RunGitAsync(repoPath, $"rev-parse --verify origin/{branch}");
        if (string.IsNullOrWhiteSpace(remote))
            return await RunGitAsync(repoPath, "diff --name-only HEAD~1 HEAD");
        return await RunGitAsync(repoPath, $"diff --name-only origin/{branch}..HEAD");
    }

    private async Task<string> GetUncommittedAsync(string repoPath)
    {
        var staged = await RunGitAsync(repoPath, "diff --name-only --cached");
        var unstaged = await RunGitAsync(repoPath, "diff --name-only");
        var untracked = await RunGitAsync(repoPath, "ls-files --others --exclude-standard");
        return string.Join("\n", new[] { staged, unstaged, untracked }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public async Task<Dictionary<string, string>> GetFileContentsAsync(string repoPath, List<string> files)
    {
        var contents = new Dictionary<string, string>();
        foreach (var file in files)
        {
            var fullPath = Path.Combine(repoPath, file);
            if (!File.Exists(fullPath)) continue;
            if (BinaryExtensions.Contains(Path.GetExtension(file))) continue;

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
        ".html" => "HTML",
        ".css" => "CSS",
        ".json" => "JSON",
        ".xml" => "XML",
        ".yml" or ".yaml" => "YAML",
        ".md" => "Markdown",
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
        catch { return string.Empty; }
    }
}