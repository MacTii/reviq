namespace Reviq.Infrastructure.Configuration;

public sealed class HuggingFaceOptions
{
    public const string Section = "HuggingFace";

    public string BaseUrl { get; init; } = "https://huggingface.co";
    public string ApiBaseUrl { get; init; } = "https://huggingface.co/api";
    public string UserAgent { get; init; } = "Reviq/1.0";
    public int SearchTimeoutSeconds { get; init; } = 30;
    public int DownloadTimeoutHours { get; init; } = 2;

    public string SearchUrl(string query, int limit) =>
        $"{ApiBaseUrl}/models?search={Uri.EscapeDataString(query)}&filter=gguf&sort=downloads&direction=-1&limit={limit}&full=false";

    public string RepoMetaUrl(string repo) =>
        $"{ApiBaseUrl}/models/{repo.Replace(" ", "%20")}?files_metadata=true";

    public string ResolveFileUrl(string repo, string filePath) =>
        $"{BaseUrl}/{repo}/resolve/main/{filePath}";

    public string ResolveFileUrlEncoded(string repo, string fileName) =>
        $"{BaseUrl}/{repo}/resolve/main/{Uri.EscapeDataString(fileName)}";
}