namespace Reviq.Domain.Entities;

public class FileReview
{
    public string FilePath { get; set; } = "";
    public string Language { get; set; } = "";
    public List<ReviewIssue> Issues { get; set; } = new();
    public int Score { get; set; }
}

