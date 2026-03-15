using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reviq.Application.Interfaces;
using Reviq.Infrastructure.Configuration;
using Reviq.Infrastructure.LocalAI.HuggingFace;
using Reviq.Infrastructure.LocalAI.Models;
using System.Collections.Concurrent;

namespace Reviq.Infrastructure.LocalAI.Services;

public sealed class LocalAIService : ILocalAIService
{
    private readonly string _modelsDir;
    private readonly HuggingFaceClient _hfClient;
    private readonly ILogger<LocalAIService> _logger;

    private static readonly ConcurrentDictionary<string, DownloadStatus> Downloads = new();

    private static readonly HuggingFaceModelDefinition[] RecommendedModels =
    {
        new("Qwen2.5-Coder 7B (Q4)",  "Qwen/Qwen2.5-Coder-7B-Instruct-GGUF",
            "qwen2.5-coder-7b-instruct-q4_k_m.gguf",  4_300_000_000L, "rec.qwen7b"),
        new("Qwen2.5-Coder 14B (Q4)", "Qwen/Qwen2.5-Coder-14B-Instruct-GGUF",
            "qwen2.5-coder-14b-instruct-q4_k_m.gguf", 8_700_000_000L, "rec.qwen14b"),
        new("DeepSeek Coder V2 Lite", "bartowski/DeepSeek-Coder-V2-Lite-Instruct-GGUF",
            "DeepSeek-Coder-V2-Lite-Instruct-Q4_K_M.gguf", 9_700_000_000L, "rec.deepseek"),
        new("Phi-3.5 Mini (Q4)",      "bartowski/Phi-3.5-mini-instruct-GGUF",
            "Phi-3.5-mini-instruct-Q4_K_M.gguf",      2_200_000_000L, "rec.phi"),
    };

    public string ModelsDirectory => _modelsDir;

    public LocalAIService(IOptions<LocalAIOptions> options, HuggingFaceClient hfClient, ILogger<LocalAIService> logger)
    {
        _modelsDir = options.Value.ModelsDir;
        _hfClient = hfClient;
        _logger = logger;
        Directory.CreateDirectory(_modelsDir);
    }

    public Task<LocalAIModelsResult> GetInstalledModelsAsync()
    {
        var models = Directory.GetFiles(_modelsDir, "*.gguf")
            .Select(f => new InstalledModel(
                Path.GetFileName(f),
                new FileInfo(f).Length,
                Math.Round(new FileInfo(f).Length / 1_000_000.0, 1)))
            .OrderBy(m => m.FileName)
            .ToList();

        return Task.FromResult(new LocalAIModelsResult(_modelsDir, models));
    }

    public IEnumerable<RecommendedModel> GetRecommendedModels() =>
        RecommendedModels.Select(m => new RecommendedModel(
            m.Name, m.Repo, m.FileName,
            m.SizeBytes,
            m.DescriptionKey,
            Math.Round(m.SizeBytes / 1_000_000.0, 0),
            File.Exists(Path.Combine(_modelsDir, m.FileName)),
            Downloads.TryGetValue(m.FileName, out var s) && s.IsRunning));

    public async Task<HuggingFaceSearchResult> SearchHuggingFaceAsync(string query, int limit = 20)
    {
        var results = await _hfClient.SearchModelsAsync(query, limit);
        var models = results.Select(r => new HuggingFaceModelInfo(r.Id, r.Downloads, r.Likes)).ToList();
        return new HuggingFaceSearchResult(models);
    }

    public async Task<HuggingFaceRepoFilesResult> GetRepoFilesAsync(string repo)
    {
        var rawFiles = await _hfClient.GetRepoFilesAsync(repo);
        var files = rawFiles
            .OrderBy(f => f.Name)
            .Select(f => new HuggingFaceFileInfo(
                f.Name,
                f.Size > 0 ? (long?)Math.Round(f.Size / 1_000_000.0) : null,
                Math.Max(0, f.Size),
                File.Exists(Path.Combine(_modelsDir, f.Name))))
            .ToList();
        return new HuggingFaceRepoFilesResult(repo, files);
    }

    public DownloadStartResult StartDownload(string repo, string filePath)
    {
        var sanitized = filePath.Replace("\\", "/").TrimStart('/');
        var fileName = Path.GetFileName(sanitized);

        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".gguf"))
            return new DownloadStartResult(false, fileName, "Invalid filename.");

        if (Downloads.TryGetValue(fileName, out var existing) && existing.IsRunning)
            return new DownloadStartResult(false, fileName, "Already downloading.");

        var destPath = Path.Combine(_modelsDir, fileName);
        if (File.Exists(destPath))
            return new DownloadStartResult(false, fileName, "Already installed.");

        var status = new DownloadStatus(fileName);
        Downloads[fileName] = status;

        var url = _hfClient.ResolveFileUrl(repo, sanitized);
        _ = Task.Run(async () =>
        {
            try { await _hfClient.DownloadAsync(url, destPath, status); }
            catch (Exception ex) { _logger.LogError(ex, "[LocalAI] Download failed: {FileName}", fileName); }
        });

        return new DownloadStartResult(true, fileName, "Download started.");
    }

    public bool CancelDownload(string fileName)
    {
        if (!Downloads.TryGetValue(fileName, out var status)) return false;
        status.Cancel();
        return true;
    }

    public DownloadStatusResult GetDownloadStatus(string fileName)
    {
        if (!Downloads.TryGetValue(fileName, out var status))
            return new DownloadStatusResult(fileName, 0, false, false, null, 0, 0);

        return new DownloadStatusResult(
            fileName, status.Progress, status.IsRunning,
            status.IsDone, status.Error,
            status.DownloadedBytes, status.TotalBytes);
    }

    public bool DeleteModel(string fileName)
    {
        var path = Path.Combine(_modelsDir, fileName);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        _logger.LogInformation("[LocalAI] Deleted model: {FileName}", fileName);
        return true;
    }

    private sealed record HuggingFaceModelDefinition(
        string Name, string Repo, string FileName,
        long SizeBytes, string DescriptionKey);
}