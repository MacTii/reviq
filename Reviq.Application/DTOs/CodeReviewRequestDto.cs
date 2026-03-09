namespace Reviq.Application.DTOs;

public class CodeReviewRequestDto
{
    public string Code { get; set; } = "";
    public string Language { get; set; } = "C#";
    public string FileName { get; set; } = "snippet.cs";
}