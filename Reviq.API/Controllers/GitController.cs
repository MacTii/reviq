using Microsoft.AspNetCore.Mvc;
using Reviq.Application.UseCases.GetRepoInfo;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GitController(GetRepoInfoHandler handler) : ControllerBase
{
    [HttpGet("info")]
    public async Task<IActionResult> GetRepoInfo([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "path jest wymagany." });

        var info = await handler.HandleAsync(new GetRepoInfoQuery { RepoPath = path });
        return Ok(info);
    }
}
