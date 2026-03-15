namespace Reviq.Domain.Entities;

public sealed record WebhookPayload(
    string Platform,
    string Action,
    string RepoFullName,
    int PrNumber,
    string CommitSha,
    string Token);
