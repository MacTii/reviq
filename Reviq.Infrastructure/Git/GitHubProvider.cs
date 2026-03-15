using Microsoft.Extensions.Options;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;
using Reviq.Infrastructure.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Reviq.Infrastructure.Git;

public sealed class GitHubProvider(HttpClient httpClient, IOptions<GitOptions> options)
    : IGitHostProvider
{
    private readonly GitOptions _opts = options.Value;

    public async Task PostReviewCommentAsync(
        string repoFullName, int prNumber, string body, string token)
    {
        SetAuth(token);
        await httpClient.PostAsJsonAsync(
            $"{_opts.GitHub.BaseUrl}/repos/{repoFullName}/issues/{prNumber}/comments",
            new { body });
    }

    public async Task SetCommitStatusAsync(
        string repoFullName, string commitSha, bool success, string description, string token)
    {
        SetAuth(token);
        var payload = new
        {
            state = success ? "success" : "failure",
            description,
            context = _opts.StatusContext
        };
        await httpClient.PostAsJsonAsync(
            $"{_opts.GitHub.BaseUrl}/repos/{repoFullName}/statuses/{commitSha}",
            payload);
    }

    public async Task<List<PrFile>> GetPrFilesAsync(
        string repoFullName, int prNumber, string token)
    {
        SetAuth(token);
        var response = await httpClient.GetAsync(
            $"{_opts.GitHub.BaseUrl}/repos/{repoFullName}/pulls/{prNumber}/files");

        if (!response.IsSuccessStatusCode) return new();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray().Select(f => new PrFile(
            FileName: f.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
            Patch: f.TryGetProperty("patch", out var p) ? p.GetString() ?? "" : "",
            RawUrl: f.TryGetProperty("raw_url", out var ru) ? ru.GetString() ?? "" : "",
            Status: f.TryGetProperty("status", out var s) ? s.GetString() ?? "" : ""
        )).ToList();
    }

    private void SetAuth(string token)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(_opts.UserAgent);
    }
}