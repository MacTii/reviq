namespace Reviq.Infrastructure.LocalAI.Models;

public sealed class DownloadStatus
{
    public string FileName { get; }
    public int Progress { get; set; }
    public bool IsRunning { get; set; } = true;
    public bool IsDone { get; set; }
    public string? Error { get; set; }
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }

    private readonly CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken => _cts.Token;

    public DownloadStatus(string fileName) => FileName = fileName;
    public void Cancel() => _cts.Cancel();
}
