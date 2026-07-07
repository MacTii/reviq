using Microsoft.Extensions.Options;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;
using Reviq.Infrastructure.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace Reviq.Infrastructure.Git;

public sealed class GitLabProvider(HttpClient httpClient, IOptions<GitOptions> options)
    : IGitHostProvider
{
    private readonly GitOptions _opts = options.Value;

    public async Task PostReviewCommentAsync(
        string repoFullName, int prNumber, string body, string token)
    {
        SetAuth(token);
        var encoded = Uri.EscapeDataString(repoFullName);
        await httpClient.PostAsJsonAsync(
            $"{_opts.GitLab.BaseUrl}/projects/{encoded}/merge_requests/{prNumber}/notes",
            new { body });
    }

    public async Task SetCommitStatusAsync(
        string repoFullName, string commitSha, bool success, string description, string token)
    {
        SetAuth(token);
        var encoded = Uri.EscapeDataString(repoFullName);
        var payload = new
        {
            state = success ? "success" : "failed",
            description,
            name = _opts.StatusContext
        };
        await httpClient.PostAsJsonAsync(
            $"{_opts.GitLab.BaseUrl}/projects/{encoded}/statuses/{commitSha}",
            payload);
    }

    public async Task<List<PrFile>> GetPrFilesAsync(
        string repoFullName, int prNumber, string token)
    {
        SetAuth(token);
        var encoded = Uri.EscapeDataString(repoFullName);
        var response = await httpClient.GetAsync(
            $"{_opts.GitLab.BaseUrl}/projects/{encoded}/merge_requests/{prNumber}/diffs");

        if (!response.IsSuccessStatusCode) return new();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray().Select(f => new PrFile(
            FileName: f.TryGetProperty("new_path", out var fn) ? fn.GetString() ?? "" : "",
            Patch: f.TryGetProperty("diff", out var p) ? p.GetString() ?? "" : "",
            RawUrl: "",
            Status: f.TryGetProperty("new_file", out var nf) && nf.GetBoolean() ? "added" : "modified"
        )).ToList();
    }

    private void SetAuth(string token)
    {
        httpClient.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
        httpClient.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
    }
}