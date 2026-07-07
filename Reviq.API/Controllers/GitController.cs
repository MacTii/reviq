using Mediator;
using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Features.Git.Queries;
using Reviq.Domain.Enums;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class GitController(IMediator mediator) : ControllerBase
{
    [HttpGet("info")]
    public async Task<IActionResult> GetRepoInfo(
        [FromQuery] string path,
        [FromQuery] DiffScope diffScope = DiffScope.LastCommit,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new GetRepoInfoQuery(path, diffScope), ct));
}
