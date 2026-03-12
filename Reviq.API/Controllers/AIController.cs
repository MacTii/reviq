using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Interfaces;
using Reviq.Domain.Enums;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/ai")]
public class AIController(IAIProviderFactory providerFactory) : ControllerBase
{
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
                name = p.NameString,
                label = p.NameString,
                type = p.TypeString,
                available,
                hasConfig = p.HasConfig
            });
        }

        var current = providerFactory.GetCurrent();
        return Ok(new
        {
            providers = result,
            currentProvider = current.Name.ToString()
        });
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] string provider = "Ollama")
    {
        if (!Enum.TryParse<ProviderName>(provider, ignoreCase: true, out var name))
            return BadRequest($"Unknown provider: {provider}");

        var p = providerFactory.GetProvider(name);
        var models = await p.GetAvailableModelsAsync();
        return Ok(new { provider = name.ToString(), models });
    }

    [HttpPost("provider")]
    public IActionResult SetProvider([FromBody] SetProviderRequest req)
    {
        if (!Enum.TryParse<ProviderName>(req.Provider, ignoreCase: true, out var name))
            return BadRequest($"Unknown provider: {req.Provider}");

        providerFactory.SetCurrent(name);

        var provider = providerFactory.GetCurrent();
        if (!string.IsNullOrWhiteSpace(req.Model))
            provider.SetModel(req.Model);

        return Ok(new { success = true, provider = name.ToString(), model = provider.CurrentModel });
    }

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