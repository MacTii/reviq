using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Interfaces;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OllamaController(IAIProvider aiProvider) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var available = await aiProvider.IsAvailableAsync();
        var models = available ? await aiProvider.GetAvailableModelsAsync() : new();

        return Ok(new
        {
            available,
            models,
            currentModel = aiProvider.CurrentModel,
            endpoint = "http://localhost:11434"
        });
    }
}
