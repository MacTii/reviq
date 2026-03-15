using Mediator;
using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Features.Reviews.Queries;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HistoryController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var results = await mediator.Send(new GetAllReviewsQuery(limit), ct);
        var items = results.Select(r => new
        {
            r.ReviewId,
            r.CreatedAt,
            r.Label,
            r.Source,
            r.Summary.OverallScore,
            r.Summary.Critical,
            r.Summary.Warnings,
            r.Summary.Info,
            FileCount = r.Files.Count
        });
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetReviewByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }
}