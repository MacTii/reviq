using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Reviq.Infrastructure.Configuration;
using Reviq.Infrastructure.LocalAI.Models;
using System.Text.Json;

namespace Reviq.Infrastructure.LocalAI.HuggingFace;

public sealed class HuggingFaceClient
{
    private readonly IHttpClientFactory _factory;
    private readonly HuggingFaceOptions _options;

    public HuggingFaceClient(IHttpClientFactory factory, IOptions<HuggingFaceOptions> options)
    {
        _factory = factory;
        _options = options.Value;
    }

    public async Task<List<(string Id, long Downloads, int Likes)>> SearchModelsAsync(
        string query, int limit = 20)
    {
        using var http = _factory.CreateClient("HuggingFace");
        var r = await http.GetAsync(_options.SearchUrl(query, limit));
        r.EnsureSuccessStatusCode();

        var json = await r.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(m => (
            Id: m.GetProperty("id").GetString() ?? "",
            Downloads: m.TryGetProperty("downloads", out var d) ? d.GetInt64() : 0L,
            Likes: m.TryGetProperty("likes", out var l) ? l.GetInt32() : 0
        )).ToList();
    }

    public async Task<List<(string Name, long Size)>> GetRepoFilesAsync(string repo)
    {
        using var http = _factory.CreateClient("HuggingFace");
        var r = await http.GetAsync(_options.RepoMetaUrl(repo));
        r.EnsureSuccessStatusCode();

        var json = await r.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("siblings", out var siblings))
            return new();

        var fileList = new List<(string name, long size)>();
        foreach (var f in siblings.EnumerateArray())
        {
            if (!f.TryGetProperty("rfilename", out var fn)) continue;
            var name = fn.GetString() ?? "";
            if (!name.EndsWith(".gguf")) continue;

            var size = 0L;
            if (f.TryGetProperty("lfs", out var lfs) &&
                lfs.TryGetProperty("size", out var ls) &&
                ls.ValueKind == JsonValueKind.Number)
                size = ls.GetInt64();
            else if (f.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number)
                size = s.GetInt64();

            if (size == 0) size = -1;
            fileList.Add((name, size));
        }

        using var sem = new SemaphoreSlim(4);
        var tasks = fileList.Select(async entry =>
        {
            if (entry.size != -1) return (entry.name, entry.size);
            await sem.WaitAsync();
            try
            {
                using var headClient = _factory.CreateClient("HuggingFace");
                var hUrl = _options.ResolveFileUrlEncoded(repo, entry.name);
                var resp = await headClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, hUrl));
                return (entry.name, resp.Content.Headers.ContentLength ?? 0L);
            }
            catch { return (entry.name, 0L); }
            finally { sem.Release(); }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    public string ResolveFileUrl(string repo, string filePath) =>
        _options.ResolveFileUrl(repo, filePath);

    public async Task DownloadAsync(string url, string destPath, DownloadStatus status)
    {
        var tmpPath = destPath + ".tmp";
        try
        {
            using var http = _factory.CreateClient("HuggingFaceDownload");
            using var response = await http.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, status.CancellationToken);
            response.EnsureSuccessStatusCode();
            status.TotalBytes = response.Content.Headers.ContentLength ?? 0;

            await using var stream = await response.Content.ReadAsStreamAsync(status.CancellationToken);
            await using var file = File.Create(tmpPath);
            var buffer = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(buffer, status.CancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), status.CancellationToken);
                status.DownloadedBytes += read;
                if (status.TotalBytes > 0)
                    status.Progress = (int)(status.DownloadedBytes * 100 / status.TotalBytes);
            }

            file.Close();
            File.Move(tmpPath, destPath, overwrite: true);
            status.Progress = 100; status.IsDone = true; status.IsRunning = false;
        }
        catch (OperationCanceledException)
        {
            status.IsRunning = false; status.Error = "Cancelled";
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
        catch (Exception ex)
        {
            status.IsRunning = false; status.Error = ex.Message;
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            throw;
        }
    }
}