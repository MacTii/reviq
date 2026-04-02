using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Interfaces;
using Reviq.API.Requests;
using Reviq.Domain.Enums;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AIController(
    IAIProviderFactory providerFactory,
    ILocalAIService localAIService) : ControllerBase
{
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var configured = providerFactory.GetConfiguredProviders();
        var current = providerFactory.GetCurrent();

        var providers = configured.Select(p => new
        {
            name = p.Name.ToString(),
            label = p.Name.ToString(),
            type = p.Type.ToString().ToLower(),
            hasConfig = p.HasConfig
        });

        return Ok(new { providers, currentProvider = current.Name.ToString() });
    }

    [HttpGet("providers/{name}/status")]
    public async Task<IActionResult> GetProviderStatus(string name, CancellationToken ct)
    {
        if (!Enum.TryParse<ProviderName>(name, ignoreCase: true, out var providerName))
            return BadRequest($"Unknown provider: {name}");

        // LocalAI — zawsze dostępny (lokalny provider), modele opcjonalne
        if (providerName == ProviderName.LocalAI)
        {
            var installed = await localAIService.GetInstalledModelsAsync();
            var models = installed.Models.Select(m => m.FileName).ToList();
            return Ok(new { name, available = true, models });
        }

        // Jeśli provider wymaga konfiguracji (np. API key) a nie jest skonfigurowany,
        // traktujemy go jako niedostępny zamiast wykonywać zewnętrzne żądanie
        var configured = providerFactory.GetConfiguredProviders()
            .FirstOrDefault(p => p.Name == providerName);
        if (configured != null && !configured.HasConfig)
            return Ok(new { name, available = false, models = new List<string>() });

        var provider = providerFactory.GetProvider(providerName);

        // Krótki timeout dla sprawdzania dostępności - nie blokuj UI
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        bool available;
        try { available = await provider.IsAvailableAsync(); }
        catch { available = false; }

        var modelList = available ? await provider.GetAvailableModelsAsync() : new List<string>();
        return Ok(new { name, available, models = modelList });
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
        if (!string.IsNullOrWhiteSpace(req.Model))
            providerFactory.SetModel(req.Model);

        return Ok(new
        {
            success = true,
            provider = name.ToString(),
            model = providerFactory.GetCurrent().CurrentModel
        });
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