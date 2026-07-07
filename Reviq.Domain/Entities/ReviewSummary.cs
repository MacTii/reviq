namespace Reviq.Domain.Entities;

public sealed class ReviewSummary
{
    public int TotalIssues { get; }
    public int Critical { get; }
    public int Warnings { get; }
    public int Info { get; }
    public int OverallScore { get; }
    public string GeneralFeedback { get; }

    public ReviewSummary(int totalIssues, int critical, int warnings, int info, int overallScore, string generalFeedback)
    {
        TotalIssues = totalIssues;
        Critical = critical;
        Warnings = warnings;
        Info = info;
        OverallScore = overallScore;
        GeneralFeedback = generalFeedback;
    }
}
