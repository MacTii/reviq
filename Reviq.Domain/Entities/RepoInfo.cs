namespace Reviq.Domain.Entities;

public class RepoInfo
{
    public bool IsValid { get; set; }
    public string Branch { get; set; } = "";
    public string LatestCommit { get; set; } = "";
    public string CommitMessage { get; set; } = "";
    public List<string> ChangedFiles { get; set; } = new();
    public string? Error { get; set; }
}

