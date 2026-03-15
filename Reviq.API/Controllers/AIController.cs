using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Interfaces;
using Reviq.API.Requests;
using Reviq.Domain.Enums;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AIController(IAIProviderFactory providerFactory) : ControllerBase
{
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
    {
        var configured = providerFactory.GetConfiguredProviders();
        var current = providerFactory.GetCurrent();

        var providers = await Task.WhenAll(configured.Select(async p =>
        {
            var provider = providerFactory.GetProvider(p.Name);
            var available = p.HasConfig && await provider.IsAvailableAsync();
            return new
            {
                name = p.Name.ToString(),
                label = p.Name.ToString(),
                type = p.Type.ToString().ToLower(),
                available,
                hasConfig = p.HasConfig
            };
        }));

        return Ok(new { providers, currentProvider = current.Name.ToString() });
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] string provider = "LocalAI")
    {
        if (!Enum.TryParse<ProviderName>(provider, ignoreCase: true, out var name))
            return BadRequest($"Unknown provider: {provider}");

        var models = await providerFactory.GetProvider(name).GetAvailableModelsAsync();
        return Ok(new { provider = name.ToString(), models });
    }

    [HttpPost("provider")]
    public IActionResult SetProvider([FromBody] SetProviderRequest req)
    {
        if (!Enum.TryParse<ProviderName>(req.Provider, ignoreCase: true, out var name))
            return BadRequest($"Unknown provider: {req.Provider}");

        providerFactory.SetCurrent(name);
        if (!string.IsNullOrWhiteSpace(req.Model)) providerFactory.SetModel(req.Model);
        return Ok(new { success = true, provider = name.ToString(), model = providerFactory.GetCurrent().CurrentModel });
    }

    [HttpPost("model")]
    public IActionResult SetModel([FromBody] SetModelRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Model))
            return BadRequest("Model is required.");
        providerFactory.SetModel(req.Model);
        return Ok(new { success = true, model = req.Model });
    }
}