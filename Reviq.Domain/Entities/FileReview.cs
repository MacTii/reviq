namespace Reviq.Domain.Entities;

public sealed class FileReview
{
    public string FilePath { get; }
    public string Language { get; }
    public int Score { get; }
    public IReadOnlyList<ReviewIssue> Issues { get; }

    public FileReview(string filePath, string language, int score, IReadOnlyList<ReviewIssue> issues)
    {
        FilePath = filePath;
        Language = language;
        Score = score;
        Issues = issues;
    }
}
