using Microsoft.AspNetCore.Mvc;
using Reviq.Application.UseCases.GetRepoInfo;
using Reviq.Domain.Enums;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GitController(GetRepoInfoHandler handler) : ControllerBase
{
    [HttpGet("info")]
    public async Task<IActionResult> GetRepoInfo([FromQuery] string path, [FromQuery] DiffScope diffScope = DiffScope.LastCommit)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "path jest wymagany." });

        var info = await handler.HandleAsync(new GetRepoInfoQuery
        {
            RepoPath = path,
            Scope = diffScope
        });
        return Ok(info);
    }
}