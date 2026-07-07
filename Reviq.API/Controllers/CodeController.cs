using Mediator;
using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Features.Reviews.Commands;
using Reviq.Application.Requests;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CodeController(IMediator mediator) : ControllerBase
{
    [HttpPost("review")]
    public async Task<IActionResult> Review(
        [FromBody] CodeReviewRequest request,
        CancellationToken ct)
        => Ok(await mediator.Send(new RunCodeReviewCommand(request), ct));

    [HttpPost("review-batch")]
    public async Task<IActionResult> ReviewBatch(
        [FromBody] BatchReviewRequest request,
        [FromQuery] string? label,
        CancellationToken ct)
    {
        var batchLabel = label
            ?? (request.Files.Count == 1
                ? request.Files[0].FileName
                : $"{request.Files.Count} files");

        return Ok(await mediator.Send(new RunBatchReviewCommand(request, batchLabel), ct));
    }
}