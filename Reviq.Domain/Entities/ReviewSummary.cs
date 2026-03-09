namespace Reviq.Domain.Entities;

public class ReviewSummary
{
    public int TotalIssues { get; set; }
    public int Critical { get; set; }
    public int Warnings { get; set; }
    public int Info { get; set; }
    public int OverallScore { get; set; }
    public string? GeneralFeedback { get; set; }
}