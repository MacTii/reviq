using Microsoft.AspNetCore.Mvc;
using Reviq.Application.DTOs;
using Reviq.Application.UseCases.RunReview;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CodeController(RunReviewHandler handler) : ControllerBase
{
    /// <summary>POST /api/code/review — pojedynczy plik (legacy)</summary>
    [HttpPost("review")]
    public async Task<IActionResult> Review(
        [FromBody] CodeReviewRequest request,
        [FromQuery] string? model = null)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Pole 'code' jest wymagane." });

        if (!string.IsNullOrWhiteSpace(model))
            handler.SetModel(model);

        var result = await handler.HandleRawCodeAsync(new CodeReviewRequestDto
        {
            Code = request.Code,
            Language = request.Language ?? "Unknown",
            FileName = request.FileName ?? "snippet.cs"
        });

        return Ok(result);
    }

    /// <summary>POST /api/code/review-batch — wiele plików jako jedna sesja</summary>
    [HttpPost("review-batch")]
    public async Task<IActionResult> ReviewBatch(
        [FromBody] BatchReviewRequest request,
        [FromQuery] string? model = null)
    {
        if (request.Files == null || request.Files.Count == 0)
            return BadRequest(new { error = "Brak plików do analizy." });

        if (!string.IsNullOrWhiteSpace(model))
            handler.SetModel(model);

        var dtos = request.Files.Select(f => new CodeReviewRequestDto
        {
            Code = f.Code,
            Language = f.Language ?? "Unknown",
            FileName = f.FileName ?? "snippet.cs"
        }).ToList();

        var label = request.Files.Count == 1
            ? request.Files[0].FileName ?? "snippet"
            : $"{request.Files.Count} pliki: {string.Join(", ", request.Files.Take(3).Select(f => f.FileName))}";

        var result = await handler.HandleBatchAsync(dtos, label);
        return Ok(result);
    }
}

public record CodeReviewRequest(string Code, string? Language, string? FileName);
public record BatchFileEntry(string Code, string? Language, string? FileName);
public record BatchReviewRequest(List<BatchFileEntry> Files);