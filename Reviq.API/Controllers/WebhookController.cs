using Microsoft.AspNetCore.Mvc;
using Reviq.API.Responses;
using Reviq.API.Webhooks;
using Reviq.Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/webhook")]
public sealed class WebhookController(
    IWebhookQueue queue,
    IConfiguration config,
    ILogger<WebhookController> logger) : ControllerBase
{
    [HttpPost("github")]
    public async Task<IActionResult> GitHub()
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();

        if (!VerifyGitHubSignature(body))
        {
            logger.LogWarning("Rejected GitHub webhook: invalid or missing signature.");
            return Unauthorized();
        }

        var payload = ParseGitHubPayload(body);
        if (payload is null) return Ok();
        await queue.EnqueueAsync(payload, HttpContext.RequestAborted);
        return Ok(new WebhookReceivedResponse(true));
    }

    [HttpPost("gitlab")]
    public async Task<IActionResult> GitLab()
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync();

        if (!VerifyGitLabToken())
        {
            logger.LogWarning("Rejected GitLab webhook: invalid or missing token.");
            return Unauthorized();
        }

        var payload = ParseGitLabPayload(body);
        if (payload is null) return Ok();
        await queue.EnqueueAsync(payload, HttpContext.RequestAborted);
        return Ok(new WebhookReceivedResponse(true));
    }

    // Verifies the GitHub HMAC-SHA256 payload signature (X-Hub-Signature-256) against the
    // configured Git:GitHub:WebhookSecret. If no secret is configured, verification is skipped
    // (matches how other optional secrets behave in this app) but a warning is logged so the
    // gap is visible in logs rather than silently permissive.
    private bool VerifyGitHubSignature(string body)
    {
        var secret = config["Git:GitHub:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogWarning("Git:GitHub:WebhookSecret is not configured — accepting GitHub webhook without signature verification.");
            return true;
        }

        var header = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!header.StartsWith("sha256=", StringComparison.Ordinal)) return false;

        var expectedHex = header["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(expectedHex));
    }

    // GitLab webhooks authenticate with a plain shared secret token (X-Gitlab-Token), not an
    // HMAC signature. Same "skip if unconfigured, but log" behavior as the GitHub check above.
    private bool VerifyGitLabToken()
    {
        var secret = config["Git:GitLab:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogWarning("Git:GitLab:WebhookSecret is not configured — accepting GitLab webhook without token verification.");
            return true;
        }

        var token = Request.Headers["X-Gitlab-Token"].ToString();
        if (string.IsNullOrEmpty(token)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
    }

    private WebhookPayload? ParseGitHubPayload(string body)
    {
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
            Token: config["Git:GitHub:Token"] ?? "");
    }

    private WebhookPayload? ParseGitLabPayload(string body)
    {
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
            Token: config["Git:GitLab:Token"] ?? "");
    }
}
