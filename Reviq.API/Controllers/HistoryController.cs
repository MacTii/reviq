using Microsoft.AspNetCore.Mvc;
using Reviq.Domain.Interfaces;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HistoryController(IReviewRepository repository) : ControllerBase
{
    /// <summary>GET /api/history?limit=20</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 50)
    {
        var results = await repository.GetAllAsync(limit);
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

    /// <summary>GET /api/history/{id}</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await repository.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }
}