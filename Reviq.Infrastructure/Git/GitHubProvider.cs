using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Infrastructure.Git;

public class GitHubProvider(HttpClient httpClient) : IGitHostProvider
{
    public async Task PostReviewCommentAsync(string repoFullName, int prNumber, string body, string token)
    {
        SetAuth(token);
        var payload = new { body };
        await httpClient.PostAsJsonAsync(
            $"https://api.github.com/repos/{repoFullName}/issues/{prNumber}/comments",
            payload);
    }

    public async Task SetCommitStatusAsync(string repoFullName, string commitSha, bool success, string description, string token)
    {
        SetAuth(token);
        var payload = new
        {
            state = success ? "success" : "failure",
            description,
            context = "Reviq / AI Code Review"
        };
        await httpClient.PostAsJsonAsync(
            $"https://api.github.com/repos/{repoFullName}/statuses/{commitSha}",
            payload);
    }

    public async Task<List<PrFile>> GetPrFilesAsync(string repoFullName, int prNumber, string token)
    {
        SetAuth(token);
        var response = await httpClient.GetAsync(
            $"https://api.github.com/repos/{repoFullName}/pulls/{prNumber}/files");

        if (!response.IsSuccessStatusCode) return new();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray().Select(f => new PrFile
        {
            FileName = f.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
            Patch = f.TryGetProperty("patch", out var p) ? p.GetString() ?? "" : "",
            RawUrl = f.TryGetProperty("raw_url", out var ru) ? ru.GetString() ?? "" : "",
            Status = f.TryGetProperty("status", out var s) ? s.GetString() ?? "" : ""
        }).ToList();
    }

    private void SetAuth(string token)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Reviq");
    }
}