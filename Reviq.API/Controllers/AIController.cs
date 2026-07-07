using Mediator;
using Microsoft.AspNetCore.Mvc;
using Reviq.API.Requests;
using Reviq.API.Responses;
using Reviq.Application.Features.AI.Queries;
using Reviq.Application.Interfaces;
using Reviq.Domain.Enums;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AIController(
    IMediator mediator,
    IAIProviderFactory providerFactory) : ControllerBase
{
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var configured = providerFactory.GetConfiguredProviders();
        var current = providerFactory.GetCurrent();

        var providers = configured
            .Select(p => new ProviderSummaryResponse(
                p.Name.ToString(), p.Name.ToString(), p.Type.ToString().ToLowerInvariant(), p.HasConfig))
            .ToList();

        return Ok(new ProvidersResponse(providers, current.Name.ToString()));
    }

    [HttpGet("providers/{name}/status")]
    public async Task<IActionResult> GetProviderStatus(string name, CancellationToken ct)
    {
        var status = await mediator.Send(new GetProviderStatusQuery(name), ct);
        return status is null ? BadRequest($"Unknown provider: {name}") : Ok(status);
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] string provider = "LocalAI")
    {
        if (!Enum.TryParse<ProviderName>(provider, ignoreCase: true, out var name))
            return BadRequest($"Unknown provider: {provider}");

        var models = await providerFactory.GetProvider(name).GetAvailableModelsAsync();
        return Ok(new ModelsResponse(name.ToString(), models));
    }

    [HttpPost("provider")]
    public IActionResult SetProvider([FromBody] SetProviderRequest req)
    {
        if (!Enum.TryParse<ProviderName>(req.Provider, ignoreCase: true, out var name))
            return BadRequest($"Unknown provider: {req.Provider}");

        providerFactory.SetCurrent(name);
        if (!string.IsNullOrWhiteSpace(req.Model))
            providerFactory.SetModel(req.Model);

        return Ok(new SetProviderResponse(true, name.ToString(), providerFactory.GetCurrent().CurrentModel));
    }

    [HttpPost("model")]
    public IActionResult SetModel([FromBody] SetModelRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Model))
            return BadRequest("Model is required.");

        providerFactory.SetModel(req.Model);
        return Ok(new SetModelResponse(true, req.Model));
    }
}
