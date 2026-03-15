namespace Reviq.Domain.Entities;

public sealed class RepoInfo
{
    public bool IsValid { get; }
    public string Branch { get; }
    public string LatestCommit { get; }
    public string CommitMessage { get; }
    public IReadOnlyList<string> ChangedFiles { get; }
    public string? Error { get; }

    private RepoInfo(bool isValid, string branch, string latestCommit, string commitMessage,
        IReadOnlyList<string> changedFiles, string? error)
    {
        IsValid = isValid;
        Branch = branch;
        LatestCommit = latestCommit;
        CommitMessage = commitMessage;
        ChangedFiles = changedFiles;
        Error = error;
    }

    public static RepoInfo Valid(string branch, string latestCommit, string commitMessage,
        IReadOnlyList<string> changedFiles)
        => new(true, branch, latestCommit, commitMessage, changedFiles, null);

    public static RepoInfo Invalid(string error)
        => new(false, "", "", "", Array.Empty<string>(), error);
}
