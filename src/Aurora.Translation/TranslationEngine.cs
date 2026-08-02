using Aurora.Core;
using Aurora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aurora.Translation;

public sealed class TranslationEngine : ITranslationEngine, IDisposable
{
    private readonly IAppSettings _settings;
    private readonly ILogger<TranslationEngine> _logger;
    private readonly ModelTranslator _modelTranslator;
    private Dictionary<string, string> _private;
    private Dictionary<string, string> _generic;
    private readonly DictionaryWatcher _privateWatcher;
    private readonly DictionaryWatcher _genericWatcher;
    private readonly object _lock = new();

    /// <summary>Fired when either dictionary file is hot-reloaded. Aurora.App subscribes to publish DictionaryReloaded.</summary>
    public event EventHandler? DictionaryHotReloaded;

    public TranslationEngine(IAppSettings settings, IModelManager modelManager, ILogger<TranslationEngine> logger)
    {
        _settings = settings;
        _logger = logger;

        _modelTranslator = new ModelTranslator(modelManager, logger);

        _private = DictionaryLoader.Load(settings.PrivateDictionaryPath, logger);
        _generic = DictionaryLoader.Load(settings.GenericDictionaryPath, logger);

        _privateWatcher = new DictionaryWatcher(settings.PrivateDictionaryPath, () => Reload(isPrivate: true));
        _genericWatcher = new DictionaryWatcher(settings.GenericDictionaryPath, () => Reload(isPrivate: false));
    }

    public async Task<TranslationResult> TranslateAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TranslationResult(text, TranslationSourceLevel.Verbatim);

        lock (_lock)
        {
            if (_private.TryGetValue(text, out var priv))
                return new TranslationResult(priv, TranslationSourceLevel.Private);

            if (_generic.TryGetValue(text, out var gen))
                return new TranslationResult(gen, TranslationSourceLevel.Generic);
        }

        var modelResult = await _modelTranslator.TryTranslateAsync(text, ct);
        if (modelResult is not null)
            return new TranslationResult(modelResult, TranslationSourceLevel.Model);

        return new TranslationResult(text, TranslationSourceLevel.Verbatim);
    }

    private void Reload(bool isPrivate)
    {
        var path = isPrivate ? _settings.PrivateDictionaryPath : _settings.GenericDictionaryPath;
        var fresh = DictionaryLoader.Load(path, _logger);
        lock (_lock)
        {
            if (isPrivate) _private = fresh;
            else _generic = fresh;
        }
        _logger.LogInformation("Dictionary reloaded: {Path}", path);
        DictionaryHotReloaded?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _privateWatcher.Dispose();
        _genericWatcher.Dispose();
        _ = _modelTranslator.DisposeAsync(); // sync-safe: stub returns ValueTask.CompletedTask
    }
}
