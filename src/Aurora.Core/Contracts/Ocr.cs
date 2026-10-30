namespace Aurora.Core.Contracts;

/// <summary>A recognised line; bounds in the pixels of the frame it was read from.</summary>
public sealed record OcrLine(string Text, PixelRect Bounds);

/// <summary>Lines in reading order.</summary>
public sealed record OcrResult(IReadOnlyList<OcrLine> Lines)
{
    public static readonly OcrResult Empty = new([]);
}

public interface IOcrEngine
{
    /// <exception cref="OcrLanguageMissingException">The configured or profile language is not installed.</exception>
    Task<OcrResult> RecognizeAsync(Frame frame, CancellationToken cancellationToken);
}

public sealed class OcrLanguageMissingException : Exception
{
    public OcrLanguageMissingException(string language)
        : base($"The OCR language '{language}' is not installed. Install it from Settings > Time & language > Language & region.")
    {
        Language = language;
    }

    public string Language { get; }
}
