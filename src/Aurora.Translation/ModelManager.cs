using Aurora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aurora.Translation;

public sealed class ModelManager : IModelManager
{
    private readonly IAppSettings _settings;
    private readonly ILogger<ModelManager> _logger;
    private int _loaded;

    public bool IsLoaded => _loaded != 0;

    public ModelManager(IAppSettings settings, ILogger<ModelManager> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _loaded, 1, 0) != 0)
            return Task.CompletedTask;
        try
        {
            var modelDir = _settings.ModelCachePath;
            if (string.IsNullOrWhiteSpace(modelDir) || !Directory.Exists(modelDir))
            {
                _logger.LogWarning("Model cache path not found ({Path}) — ONNX tier unavailable.", modelDir);
                return Task.CompletedTask;
            }

            // TODO: load MarianMT ONNX InferenceSession — make async when implemented
            _logger.LogInformation("ONNX model directory found; session loading not yet implemented.");
            return Task.CompletedTask;
        }
        catch
        {
            Interlocked.Exchange(ref _loaded, 0); // allow retry on failure
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _loaded, 0);
        return ValueTask.CompletedTask;
    }
}
