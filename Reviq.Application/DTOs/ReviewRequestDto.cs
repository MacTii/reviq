namespace Reviq.Application.DTOs;

public class ReviewRequestDto
{
    public string RepoPath { get; set; } = "";
    public string? CommitHash { get; set; }
    public List<string> Files { get; set; } = new();
}
