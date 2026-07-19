namespace Reviq.Application.Interfaces;

public interface ILocalAIService
{
    Task<LocalAIModelsResult> GetInstalledModelsAsync();
    IEnumerable<RecommendedModel> GetRecommendedModels();
    Task<HuggingFaceSearchResult> SearchHuggingFaceAsync(string query, int limit = 20);
    Task<HuggingFaceRepoFilesResult> GetRepoFilesAsync(string repo);
    DownloadStartResult StartDownload(string repo, string filePath);
    bool CancelDownload(string fileName);
    DownloadStatusResult GetDownloadStatus(string fileName);
    bool DeleteModel(string fileName);
    string ModelsDirectory { get; }
}

// ── Result types ──────────────────────────────────────────────────────────────
public sealed record LocalAIModelsResult(string ModelsDir, IReadOnlyList<InstalledModel> Models);

public sealed record InstalledModel(string FileName, long Size, double SizeMb);

public sealed record RecommendedModel(
    string Name, string Repo, string FileName, long SizeBytes,
    string Description, double SizeMb, bool IsInstalled, bool IsDownloading);

public sealed record HuggingFaceSearchResult(IReadOnlyList<HuggingFaceModelInfo> Models);

public sealed record HuggingFaceModelInfo(string Id, long Downloads, int Likes);

public sealed record HuggingFaceRepoFilesResult(string Repo, IReadOnlyList<HuggingFaceFileInfo> Files);

public sealed record HuggingFaceFileInfo(string FileName, long? SizeMb, long SizeBytes, bool IsInstalled);

public sealed record DownloadStartResult(bool Started, string FileName, string Message);

public sealed record DownloadStatusResult(
    string FileName, int Progress, bool IsRunning,
    bool IsDone, string? Error, long DownloadedBytes, long TotalBytes);
