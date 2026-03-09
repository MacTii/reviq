using Microsoft.AspNetCore.Mvc;
using Reviq.Application.DTOs;
using Reviq.Application.UseCases.RunReview;
using Reviq.Infrastructure.AI;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController(
    RunReviewHandler handler,
    OllamaProvider ollamaProvider,
    ILogger<ReviewController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> StartReview(
        [FromBody] ReviewRequestDto request,
        [FromQuery] string model = "deepseek-coder-v2")
    {
        if (string.IsNullOrWhiteSpace(request.RepoPath))
            return BadRequest(new { error = "RepoPath jest wymagany." });

        ollamaProvider.SetModel(model);

        try
        {
            var result = await handler.HandleAsync(new RunReviewCommand { Request = request });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review failed");
            return StatusCode(500, new { error = "Błąd podczas analizy. Sprawdź czy Ollama działa." });
        }
    }
}
