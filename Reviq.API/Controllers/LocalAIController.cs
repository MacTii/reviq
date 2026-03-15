using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Interfaces;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/localai")]
public sealed class LocalAIController(ILocalAIService localAiService) : ControllerBase
{
    [HttpGet("Models")]
    public async Task<IActionResult> GetModels()
        => Ok(await localAiService.GetInstalledModelsAsync());

    [HttpGet("recommended")]
    public IActionResult GetRecommended()
        => Ok(localAiService.GetRecommendedModels());

    [HttpGet("hf/search")]
    public async Task<IActionResult> SearchHuggingFace(
        [FromQuery] string q = "coder gguf",
        [FromQuery] int limit = 20)
    {
        var result = await localAiService.SearchHuggingFaceAsync(q, limit);
        return Ok(result.Models);
    }

    [HttpGet("hf/files")]
    public async Task<IActionResult> GetRepoFiles([FromQuery] string repo)
    {
        if (string.IsNullOrWhiteSpace(repo)) return BadRequest("repo required");
        var result = await localAiService.GetRepoFilesAsync(repo);
        return Ok(result);
    }

    [HttpPost("download")]
    public IActionResult StartDownload([FromBody] DownloadRequest req)
    {
        var result = localAiService.StartDownload(req.Repo, req.FileName);
        return result.Started ? Ok(result) : Conflict(result);
    }

    [HttpDelete("models/{fileName}")]
    public IActionResult DeleteModel(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        return localAiService.DeleteModel(fileName)
            ? Ok(new { success = true })
            : NotFound();
    }

    [HttpGet("download/{fileName}/status")]
    public IActionResult GetDownloadStatus(string fileName)
        => Ok(localAiService.GetDownloadStatus(Path.GetFileName(fileName)));

    [HttpPost("download/{fileName}/cancel")]
    public IActionResult CancelDownload(string fileName)
    {
        localAiService.CancelDownload(Path.GetFileName(fileName));
        return Ok(new { success = true });
    }
}

public sealed record DownloadRequest(string Repo, string FileName);
