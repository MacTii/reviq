using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Infrastructure.Git;

public class GitLabProvider(HttpClient httpClient) : IGitHostProvider
{
    public async Task PostReviewCommentAsync(string repoFullName, int prNumber, string body, string token)
    {
        SetAuth(token);
        // repoFullName = "group/project" → URL encode
        var encoded = Uri.EscapeDataString(repoFullName);
        var payload = new { body };
        await httpClient.PostAsJsonAsync(
            $"https://gitlab.com/api/v4/projects/{encoded}/merge_requests/{prNumber}/notes",
            payload);
    }

    public async Task SetCommitStatusAsync(string repoFullName, string commitSha, bool success, string description, string token)
    {
        SetAuth(token);
        var encoded = Uri.EscapeDataString(repoFullName);
        var payload = new
        {
            state = success ? "success" : "failed",
            description,
            name = "Reviq / AI Code Review"
        };
        await httpClient.PostAsJsonAsync(
            $"https://gitlab.com/api/v4/projects/{encoded}/statuses/{commitSha}",
            payload);
    }

    public async Task<List<PrFile>> GetPrFilesAsync(string repoFullName, int prNumber, string token)
    {
        SetAuth(token);
        var encoded = Uri.EscapeDataString(repoFullName);
        var response = await httpClient.GetAsync(
            $"https://gitlab.com/api/v4/projects/{encoded}/merge_requests/{prNumber}/diffs");

        if (!response.IsSuccessStatusCode) return new();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray().Select(f => new PrFile
        {
            FileName = f.TryGetProperty("new_path", out var fn) ? fn.GetString() ?? "" : "",
            Patch = f.TryGetProperty("diff", out var p) ? p.GetString() ?? "" : "",
            Status = f.TryGetProperty("new_file", out var nf) && nf.GetBoolean() ? "added" : "modified"
        }).ToList();
    }

    private void SetAuth(string token)
    {
        httpClient.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
        httpClient.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
    }
}