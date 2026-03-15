using Microsoft.Extensions.Options;
using Reviq.Infrastructure.Configuration;
using Reviq.Infrastructure.AI;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Logging;
using Reviq.Domain.Enums;

namespace Reviq.Infrastructure.AI.Providers;

public class LocalAIProvider : BaseAIProvider
{
    private readonly ILogger<LocalAIProvider> _logger;
    private readonly string _modelsDir;

    // Załadowany model trzymamy w pamięci między requestami
    private LLamaWeights? _weights;
    private ModelParams? _loadedParams;
    private string? _loadedModelPath;

    public override ProviderName Name => ProviderName.LocalAI;

    public LocalAIProvider(ILogger<LocalAIProvider> logger, IOptions<LocalAIOptions> options)
    {
        _logger = logger;
        _modelsDir = options.Value.ModelsDir;
    }

    internal override void SetModel(string model)
    {
        // model to nazwa pliku .gguf lub pełna ścieżka
        CurrentModel = model;
        // Zwolnij stary model jeśli zmieniony
        if (_loadedModelPath != ResolveModelPath(model))
            UnloadModel();
    }

    public override Task<bool> IsAvailableAsync()
    {
        // Zawsze dostępny — user może pobrać modele przez modal
        return Task.FromResult(true);
    }

    public override Task<List<string>> GetAvailableModelsAsync()
    {
        if (!Directory.Exists(_modelsDir))
            return Task.FromResult(new List<string>());

        var models = Directory.GetFiles(_modelsDir, "*.gguf")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .OrderBy(f => f)
            .ToList();

        return Task.FromResult(models);
    }

    public override async Task<string> ReviewCodeAsync(
        string code, string language, string filePath, IList<string>? categories = null)
    {
        var modelPath = ResolveModelPath(CurrentModel);

        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            throw new FileNotFoundException($"Model not found: {CurrentModel}");

        // Załaduj model jeśli nie załadowany lub zmieniony
        if (_weights is null || _loadedModelPath != modelPath)
        {
            UnloadModel();
            _logger.LogInformation("[LocalAI] Loading model: {Path}", modelPath);

            _loadedParams = new ModelParams(modelPath)
            {
                ContextSize = 8192,
                GpuLayerCount = 99,       // offload wszystko na GPU jeśli możliwe
                Threads = Math.Max(1, Environment.ProcessorCount - 2),
            };

            _weights = LLamaWeights.LoadFromFile(_loadedParams);
            _loadedModelPath = modelPath;

            _logger.LogInformation("[LocalAI] Model loaded successfully");
        }

        var prompt = BuildPrompt(code, language, filePath, categories);

        var executor = new StatelessExecutor(_weights, _loadedParams!);

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 6000,
            AntiPrompts = new[] { "```\n\n", "\n\n\n\n" },
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.05f,
                TopP = 0.85f,
                TopK = 20,
                RepeatPenalty = 1.15f,
            }
        };

        var sb = new System.Text.StringBuilder();

        await foreach (var token in executor.InferAsync(prompt, inferenceParams))
            sb.Append(token);

        return sb.ToString();
    }

    private string? ResolveModelPath(string model)
    {
        if (string.IsNullOrEmpty(model)) return null;
        // Jeśli pełna ścieżka — użyj bezpośrednio
        if (Path.IsPathRooted(model) || model.Contains(Path.DirectorySeparatorChar))
            return model;
        // Inaczej szukaj w katalogu modeli
        return Path.Combine(_modelsDir, model);
    }

    private void UnloadModel()
    {
        _weights?.Dispose();
        _weights = null;
        _loadedModelPath = null;
        _loadedParams = null;
    }

    public void Dispose() => UnloadModel();
}