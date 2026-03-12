using Reviq.Domain.Enums;

namespace Reviq.Application.DTOs;

public class ReviewRequestDto
{
    public string RepoPath { get; set; } = "";
    public string? CommitHash { get; set; }
    public List<string> Files { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public DiffScope DiffScope { get; set; } = DiffScope.LastCommit;
}