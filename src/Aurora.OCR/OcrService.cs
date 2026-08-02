using System.Runtime.InteropServices.WindowsRuntime;
using Aurora.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Aurora.OCR;

public sealed class OcrService : IOcrService
{
    private readonly ILogger<OcrService> _logger;
    private const int MaxCacheSize = 256;
    private readonly LinkedList<(string Key, string Value)> _lruList = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, string Value)>> _cacheIndex = new(256);
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, OcrEngine> _engines = new();
    private readonly object _enginesLock = new();

    public OcrService(ILogger<OcrService> logger) => _logger = logger;

    public async Task<string> RecognizeAsync(byte[] imageBytes, string language, CancellationToken ct = default)
    {
        if (imageBytes is null or { Length: 0 }) return string.Empty;
        ct.ThrowIfCancellationRequested();

        var fingerprint = VisualFingerprintProvider.Compute(imageBytes);
        var key = $"{language}:{fingerprint}";
        lock (_cacheLock)
        {
            if (_cacheIndex.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Value;
            }
        }

        var engine = GetEngine(language);
        var bitmap = await DecodeBitmapAsync(imageBytes);
        var result = await engine.RecognizeAsync(bitmap);

        lock (_cacheLock)
        {
            if (!_cacheIndex.ContainsKey(key))
            {
                if (_lruList.Count >= MaxCacheSize)
                {
                    var last = _lruList.Last!;
                    _cacheIndex.Remove(last.Value.Key);
                    _lruList.RemoveLast();
                }
                var newNode = _lruList.AddFirst((key, result.Text));
                _cacheIndex[key] = newNode;
            }
        }
        _logger.LogDebug("OCR ({Language}): {Chars} chars", language, result.Text.Length);
        return result.Text;
    }

    private static async Task<SoftwareBitmap> DecodeBitmapAsync(byte[] imageBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(imageBytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync();
    }

    private OcrEngine GetEngine(string language)
    {
        lock (_enginesLock)
        {
            if (!_engines.TryGetValue(language, out var engine))
            {
                var lang = Windows.Globalization.Language.TryGetFromTag(language)
                           ?? throw new OcrLanguageNotAvailableException(language);
                engine = OcrEngine.TryCreateFromLanguage(lang)
                         ?? throw new OcrLanguageNotAvailableException(language);
                _engines[language] = engine;
            }
            return engine;
        }
    }
}
