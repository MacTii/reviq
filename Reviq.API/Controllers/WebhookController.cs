using Microsoft.AspNetCore.Mvc;
using Reviq.Application.UseCases.HandleWebhook;
using Reviq.Domain.Entities;
using System.Text.Json;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController(
    HandleWebhookHandler handler,
    IConfiguration config,
    ILogger<WebhookController> logger) : ControllerBase
{
    [HttpPost("github")]
    public async Task<IActionResult> GitHub()
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
        var pr = root.TryGetProperty("pull_request", out var prEl) ? prEl : default;

        if (pr.ValueKind == JsonValueKind.Undefined) return Ok();

        var payload = new WebhookPayload
        {
            Platform = "github",
            Action = action,
            RepoFullName = root.TryGetProperty("repository", out var repo)
                           && repo.TryGetProperty("full_name", out var fn)
                           ? fn.GetString() ?? "" : "",
            PrNumber = pr.TryGetProperty("number", out var num) ? num.GetInt32() : 0,
            CommitSha = pr.TryGetProperty("head", out var head)
                           && head.TryGetProperty("sha", out var sha)
                           ? sha.GetString() ?? "" : "",
            Token = config["GitHub:Token"] ?? ""
        };

        _ = Task.Run(() => handler.HandleAsync(new HandleWebhookCommand { Payload = payload }));
        return Ok(new { received = true });
    }

    [HttpPost("gitlab")]
    public async Task<IActionResult> GitLab()
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var objectKind = root.TryGetProperty("object_kind", out var ok) ? ok.GetString() : "";
        if (objectKind != "merge_request") return Ok();

        var attrs = root.TryGetProperty("object_attributes", out var oa) ? oa : default;
        if (attrs.ValueKind == JsonValueKind.Undefined) return Ok();

        var action = attrs.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";

        var payload = new WebhookPayload
        {
            Platform = "gitlab",
            Action = action == "open" ? "opened" : action,
            RepoFullName = root.TryGetProperty("project", out var proj)
                           && proj.TryGetProperty("path_with_namespace", out var ns)
                           ? ns.GetString() ?? "" : "",
            PrNumber = attrs.TryGetProperty("iid", out var iid) ? iid.GetInt32() : 0,
            CommitSha = attrs.TryGetProperty("last_commit", out var lc)
                           && lc.TryGetProperty("id", out var cid)
                           ? cid.GetString() ?? "" : "",
            Token = config["GitLab:Token"] ?? ""
        };

        _ = Task.Run(() => handler.HandleAsync(new HandleWebhookCommand { Payload = payload }));
        return Ok(new { received = true });
    }
}