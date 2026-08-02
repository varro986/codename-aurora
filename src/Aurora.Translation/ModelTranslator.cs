using Aurora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aurora.Translation;

internal sealed class ModelTranslator : IAsyncDisposable
{
    private readonly IModelManager _modelManager;
    private readonly ILogger _logger;

    internal ModelTranslator(IModelManager modelManager, ILogger logger)
    {
        _modelManager = modelManager;
        _logger = logger;
    }

    internal async Task<string?> TryTranslateAsync(string text, CancellationToken ct)
    {
        await _modelManager.EnsureLoadedAsync(ct);
        if (!_modelManager.IsLoaded) return null;

        // TODO: run MarianMT ONNX inference
        _logger.LogDebug("ONNX inference not yet implemented — falling through to verbatim.");
        return null;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
