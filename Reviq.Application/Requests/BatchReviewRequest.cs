namespace Reviq.Application.Requests;

public sealed record BatchReviewRequest(IReadOnlyList<CodeReviewRequest> Files);
