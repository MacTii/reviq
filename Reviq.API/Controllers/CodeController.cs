using Microsoft.AspNetCore.Mvc;
using Reviq.Application.DTOs;
using Reviq.Application.UseCases.RunReview;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CodeController(RunReviewHandler handler) : ControllerBase
{
    [HttpPost("review")]
    public async Task<IActionResult> ReviewCode([FromBody] CodeReviewRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code jest wymagany." });

        var result = await handler.HandleRawCodeAsync(request);
        return Ok(result);
    }
}