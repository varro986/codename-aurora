using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Aurora.App;

internal sealed class PipelineExecutor
{
    private readonly ICaptureService _capture;
    private readonly IOcrService _ocr;
    private readonly ITranslationEngine _translation;
    private readonly IPublisher _publisher;
    private readonly IAppSettings _settings;
    private readonly ILogger<PipelineExecutor> _logger;

    public PipelineExecutor(
        ICaptureService capture,
        IOcrService ocr,
        ITranslationEngine translation,
        IPublisher publisher,
        IAppSettings settings,
        ILogger<PipelineExecutor> logger)
    {
        _capture = capture;
        _ocr = ocr;
        _translation = translation;
        _publisher = publisher;
        _settings = settings;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var imageBytes = _capture.CaptureScreen();
            if (imageBytes.Length == 0) return;

            var text = await _ocr.RecognizeAsync(imageBytes, _settings.SourceLanguage, cancellationToken);
            if (string.IsNullOrWhiteSpace(text)) return;

            var result = await _translation.TranslateAsync(text, cancellationToken);
            await _publisher.Publish(new TranslationReady(result), cancellationToken);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation pipeline failed.");
        }
    }
}
