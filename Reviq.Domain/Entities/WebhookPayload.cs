namespace Reviq.Domain.Entities;

public class WebhookPayload
{
    public string Platform { get; set; } = "";   // "github" | "gitlab"
    public string RepoFullName { get; set; } = "";
    public int PrNumber { get; set; }
    public string CommitSha { get; set; } = "";
    public string Action { get; set; } = "";     // "opened" | "synchronize"
    public string Token { get; set; } = "";
}