using Mediator;
using Microsoft.AspNetCore.Mvc;
using Reviq.Application.Features.Webhook.Commands;
using Reviq.Domain.Entities;
using System.Text.Json;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/webhook")]
public sealed class WebhookController(IMediator mediator, IConfiguration config) : ControllerBase
{
    [HttpPost("github")]
    public async Task<IActionResult> GitHub()
    {
        var payload = await ParseGitHubPayloadAsync();
        if (payload is null) return Ok();
        _ = Task.Run(() => mediator.Send(new HandleWebhookCommand(payload)));
        return Ok(new { received = true });
    }

    [HttpPost("gitlab")]
    public async Task<IActionResult> GitLab()
    {
        var payload = await ParseGitLabPayloadAsync();
        if (payload is null) return Ok();
        _ = Task.Run(() => mediator.Send(new HandleWebhookCommand(payload)));
        return Ok(new { received = true });
    }

    private async Task<WebhookPayload?> ParseGitHubPayloadAsync()
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("pull_request", out var pr)) return null;

        return new WebhookPayload(
            Platform: "github",
            Action: root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "",
            RepoFullName: root.TryGetProperty("repository", out var repo) &&
                          repo.TryGetProperty("full_name", out var fn) ? fn.GetString() ?? "" : "",
            PrNumber: pr.TryGetProperty("number", out var num) ? num.GetInt32() : 0,
            CommitSha: pr.TryGetProperty("head", out var head) &&
                          head.TryGetProperty("sha", out var sha) ? sha.GetString() ?? "" : "",
            Token: config["GitHub:Token"] ?? "");
    }

    private async Task<WebhookPayload?> ParseGitLabPayloadAsync()
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("object_kind", out var ok) || ok.GetString() != "merge_request")
            return null;
        if (!root.TryGetProperty("object_attributes", out var attrs)) return null;

        var action = attrs.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";

        return new WebhookPayload(
            Platform: "gitlab",
            Action: action == "open" ? "opened" : action,
            RepoFullName: root.TryGetProperty("project", out var proj) &&
                          proj.TryGetProperty("path_with_namespace", out var ns) ? ns.GetString() ?? "" : "",
            PrNumber: attrs.TryGetProperty("iid", out var iid) ? iid.GetInt32() : 0,
            CommitSha: attrs.TryGetProperty("last_commit", out var lc) &&
                          lc.TryGetProperty("id", out var cid) ? cid.GetString() ?? "" : "",
            Token: config["GitLab:Token"] ?? "");
    }
}
