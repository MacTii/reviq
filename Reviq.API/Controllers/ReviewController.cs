using Mediator;
using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Features.Reviews.Commands;
using Reviq.Application.Requests;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReviewController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> StartReview(
        [FromBody] RunReviewRequest request,
        CancellationToken ct)
        => Ok(await mediator.Send(new RunReviewCommand(request), ct));
}