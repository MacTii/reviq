using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Reviq.API.Controllers;

[ApiController]
[Route("api/localai")]
public class LocalAIController : ControllerBase
{
    private readonly string _modelsDir;
    private readonly ILogger<LocalAIController> _logger;

    private static readonly Dictionary<string, DownloadStatus> _downloads = new();

    private static readonly HuggingFaceModel[] RecommendedModels =
    {
        new("Qwen2.5-Coder 7B (Q4)",
            "Qwen/Qwen2.5-Coder-7B-Instruct-GGUF",
            "qwen2.5-coder-7b-instruct-q4_k_m.gguf",
            4_300_000_000L, "rec.qwen7b"),
        new("Qwen2.5-Coder 14B (Q4)",
            "Qwen/Qwen2.5-Coder-14B-Instruct-GGUF",
            "qwen2.5-coder-14b-instruct-q4_k_m.gguf",
            8_700_000_000L, "rec.qwen14b"),
        new("DeepSeek Coder V2 Lite (Q4)",
            "bartowski/DeepSeek-Coder-V2-Lite-Instruct-GGUF",
            "DeepSeek-Coder-V2-Lite-Instruct-Q4_K_M.gguf",
            9_700_000_000L, "rec.deepseek"),
        new("Phi-3.5 Mini (Q4)",
            "bartowski/Phi-3.5-mini-instruct-GGUF",
            "Phi-3.5-mini-instruct-Q4_K_M.gguf",
            2_200_000_000L, "rec.phi"),
    };

    public LocalAIController(IConfiguration config, ILogger<LocalAIController> logger)
    {
        _modelsDir = config["LocalAI:ModelsDir"]
                     ?? Path.Combine(AppContext.BaseDirectory, "Models");
        _logger = logger;
        Directory.CreateDirectory(_modelsDir);
    }

    [HttpGet("Models")]
    public IActionResult GetModels()
    {
        var installed = Directory.GetFiles(_modelsDir, "*.gguf")
            .Select(f => new
            {
                fileName = Path.GetFileName(f),
                size = new FileInfo(f).Length,
                sizeMb = Math.Round(new FileInfo(f).Length / 1_000_000.0, 1)
            })
            .OrderBy(f => f.fileName)
            .ToList();
        return Ok(new { modelsDir = _modelsDir, models = installed });
    }

    [HttpGet("recommended")]
    public IActionResult GetRecommended()
    {
        var result = RecommendedModels.Select(m => new
        {
            m.Name,
            m.Repo,
            m.FileName,
            m.Description,
            sizeMb = Math.Round(m.SizeBytes / 1_000_000.0, 0),
            isInstalled = System.IO.File.Exists(Path.Combine(_modelsDir, m.FileName)),
            isDownloading = _downloads.TryGetValue(m.FileName, out var s) && s.IsRunning
        });
        return Ok(result);
    }

    [HttpGet("hf/search")]
    public async Task<IActionResult> SearchHuggingFace(
        [FromQuery] string q = "coder gguf",
        [FromQuery] int limit = 20)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Reviq/1.0");
            http.Timeout = TimeSpan.FromSeconds(15);

            var url = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(q)}&filter=gguf&sort=downloads&direction=-1&limit={limit}&full=false";
            var r = await http.GetAsync(url);
            if (!r.IsSuccessStatusCode)
                return StatusCode((int)r.StatusCode, "HuggingFace API error");

            var json = await r.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var models = doc.RootElement.EnumerateArray().Select(m => new
            {
                id = m.GetProperty("id").GetString(),
                downloads = m.TryGetProperty("downloads", out var d) ? d.GetInt64() : 0,
                likes = m.TryGetProperty("likes", out var l) ? l.GetInt32() : 0,
            }).ToList();

            return Ok(models);
        }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpGet("hf/files")]
    public async Task<IActionResult> GetRepoFiles([FromQuery] string repo)
    {
        if (string.IsNullOrWhiteSpace(repo)) return BadRequest("repo required");
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Reviq/1.0");
            http.Timeout = TimeSpan.FromSeconds(30);

            var url = $"https://huggingface.co/api/models/{repo.Replace(" ", "%20")}?files_metadata=true";
            var r = await http.GetAsync(url);
            if (!r.IsSuccessStatusCode)
                return StatusCode((int)r.StatusCode, $"HuggingFace API returned {(int)r.StatusCode}");

            var json = await r.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("siblings", out var siblings))
                return Ok(new { repo, files = Array.Empty<object>() });

            var fileList = new List<(string name, long size)>();
            foreach (var f in siblings.EnumerateArray())
            {
                if (!f.TryGetProperty("rfilename", out var fnProp)) continue;
                var name = fnProp.GetString() ?? "";
                if (!name.EndsWith(".gguf")) continue;

                // Próbuj różne pola: lfs.size (LFS), size (Xet/direct)
                var size = 0L;
                if (f.TryGetProperty("lfs", out var lfs) &&
                    lfs.TryGetProperty("size", out var ls) &&
                    ls.ValueKind == JsonValueKind.Number)
                    size = ls.GetInt64();
                else if (f.TryGetProperty("size", out var s) &&
                         s.ValueKind == JsonValueKind.Number)
                    size = s.GetInt64();

                // Jeśli nadal 0 — użyj HEAD request (Xet storage nie ma rozmiaru w API)
                if (size == 0) size = -1;

                fileList.Add((name, size));
            }

            // Dla plików bez rozmiaru — HEAD request równolegle (maks 4 jednocześnie)
            using var sem = new SemaphoreSlim(4);
            using var headClient = new HttpClient();
            headClient.DefaultRequestHeaders.Add("User-Agent", "Reviq/1.0");
            headClient.Timeout = TimeSpan.FromSeconds(8);

            var tasks = fileList.Select(async entry =>
            {
                var size = entry.size;
                if (size == -1)
                {
                    await sem.WaitAsync();
                    try
                    {
                        var hUrl = $"https://huggingface.co/{repo}/resolve/main/{Uri.EscapeDataString(entry.name)}";
                        var hResp = await headClient.SendAsync(
                            new HttpRequestMessage(HttpMethod.Head, hUrl));
                        size = hResp.Content.Headers.ContentLength ?? 0;
                    }
                    catch { size = 0; }
                    finally { sem.Release(); }
                }
                return new
                {
                    fileName = entry.name,
                    sizeMb = size > 0 ? (long?)Math.Round(size / 1_000_000.0) : null,
                    sizeBytes = Math.Max(0, size),
                    isInstalled = System.IO.File.Exists(Path.Combine(_modelsDir, entry.name))
                };
            });

            var files = (await Task.WhenAll(tasks))
                .OrderBy(f => f.fileName)
                .Cast<object>()
                .ToList();

            return Ok(new { repo, files });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocalAI] GetRepoFiles failed for {Repo}", repo);
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("download")]
    public IActionResult StartDownload([FromBody] DownloadRequest req)
    {
        // filePath może być "BF16/model.gguf" — zachowaj subfolder dla URL, ale zapisz płasko
        var filePath = req.FileName.Replace("\\", "/").TrimStart('/');
        var fileName = Path.GetFileName(filePath); // tylko nazwa pliku bez subfoldera
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".gguf"))
            return BadRequest("Invalid filename");

        if (_downloads.TryGetValue(fileName, out var existing) && existing.IsRunning)
            return Ok(new { message = "Already downloading", fileName });

        var destPath = Path.Combine(_modelsDir, fileName);
        if (System.IO.File.Exists(destPath))
            return Ok(new { message = "Already installed", fileName });

        var status = new DownloadStatus(fileName);
        _downloads[fileName] = status;
        // URL używa pełnej ścieżki z subfolderem (np. BF16/model.gguf)
        var url = $"https://huggingface.co/{req.Repo}/resolve/main/{filePath}";
        _ = Task.Run(() => DownloadModelAsync(url, destPath, status));
        return Ok(new { message = "Download started", fileName, url });
    }

    [HttpDelete("models/{fileName}")]
    public IActionResult DeleteModel(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        var path = Path.Combine(_modelsDir, fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        System.IO.File.Delete(path);
        _logger.LogInformation("[LocalAI] Deleted model: {FileName}", fileName);
        return Ok(new { success = true });
    }

    [HttpGet("download/{fileName}/status")]
    public IActionResult GetDownloadStatus(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        if (!_downloads.TryGetValue(fileName, out var status))
            return Ok(new { fileName, progress = 0, isRunning = false, isDone = false });
        return Ok(new
        {
            fileName,
            status.Progress,
            status.IsRunning,
            status.IsDone,
            status.Error,
            status.DownloadedBytes,
            status.TotalBytes
        });
    }

    [HttpPost("download/{fileName}/cancel")]
    public IActionResult CancelDownload(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        if (_downloads.TryGetValue(fileName, out var status)) status.Cancel();
        return Ok(new { success = true });
    }

    private async Task DownloadModelAsync(string url, string destPath, DownloadStatus status)
    {
        var tmpPath = destPath + ".tmp";
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromHours(2);
            http.DefaultRequestHeaders.Add("User-Agent", "Reviq/1.0");

            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, status.CancellationToken);
            response.EnsureSuccessStatusCode();
            status.TotalBytes = response.Content.Headers.ContentLength ?? 0;

            await using var stream = await response.Content.ReadAsStreamAsync(status.CancellationToken);
            await using var file = System.IO.File.Create(tmpPath);
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
            System.IO.File.Move(tmpPath, destPath, overwrite: true);
            status.Progress = 100; status.IsDone = true; status.IsRunning = false;
            _logger.LogInformation("[LocalAI] Download complete: {FileName}", Path.GetFileName(destPath));
        }
        catch (OperationCanceledException)
        {
            status.IsRunning = false; status.Error = "Cancelled";
            if (System.IO.File.Exists(tmpPath)) System.IO.File.Delete(tmpPath);
        }
        catch (Exception ex)
        {
            status.IsRunning = false; status.Error = ex.Message;
            if (System.IO.File.Exists(tmpPath)) System.IO.File.Delete(tmpPath);
            _logger.LogError(ex, "[LocalAI] Download failed for {FileName}", Path.GetFileName(destPath));
        }
    }
}

public record DownloadRequest(string Repo, string FileName);
public record HuggingFaceModel(string Name, string Repo, string FileName, long SizeBytes, string Description);

public class DownloadStatus
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