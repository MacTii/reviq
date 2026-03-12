using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Interfaces;
using Reviq.Infrastructure.AI;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/ai")]
public class AIController(IAIProviderFactory providerFactory) : ControllerBase
{
    // GET /api/ai/providers — lista skonfigurowanych providerów + czy są online
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
    {
        var configured = providerFactory.GetConfiguredProviders();
        var result = new List<object>();

        foreach (var p in configured)
        {
            var provider = providerFactory.GetProvider(p.Name);
            var available = p.HasConfig && await provider.IsAvailableAsync();
            result.Add(new
            {
                name = p.Name,
                label = p.Label,
                type = p.Type,
                available,
                hasConfig = p.HasConfig
            });
        }

        var current = providerFactory.GetCurrent();
        return Ok(new
        {
            providers = result,
            currentProvider = current.ProviderName
        });
    }

    // GET /api/ai/models?provider=ollama — modele dla konkretnego providera
    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] string provider = "ollama")
    {
        var p = providerFactory.GetProvider(provider);
        var models = await p.GetAvailableModelsAsync();
        return Ok(new { provider, models });
    }

    // POST /api/ai/provider — przełącz aktywny provider
    [HttpPost("provider")]
    public IActionResult SetProvider([FromBody] SetProviderRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Provider))
            return BadRequest("Provider is required.");

        providerFactory.SetCurrent(req.Provider);

        var provider = providerFactory.GetCurrent();
        if (!string.IsNullOrWhiteSpace(req.Model))
            provider.SetModel(req.Model);

        return Ok(new { success = true, provider = req.Provider, model = provider.CurrentModel });
    }

    // POST /api/ai/model — zmień model w aktualnym providerze
    [HttpPost("model")]
    public IActionResult SetModel([FromBody] SetModelRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Model))
            return BadRequest("Model is required.");

        providerFactory.GetCurrent().SetModel(req.Model);
        return Ok(new { success = true, model = req.Model });
    }
}

public record SetProviderRequest(string Provider, string? Model = null);
public record SetModelRequest(string Model);