namespace Reviq.Application.Requests;

public sealed record CodeReviewRequest(
    string Code,
    string Language = "C#",
    string FileName = "snippet.cs");
