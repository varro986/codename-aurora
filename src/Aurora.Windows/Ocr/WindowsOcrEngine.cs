using System.Runtime.InteropServices.WindowsRuntime;
using Aurora.Core.Contracts;
using WinImaging = global::Windows.Graphics.Imaging;
using WinOcr = global::Windows.Media.Ocr;

namespace Aurora.Windows.Ocr;

/// <summary>
/// OCR on the Windows built-in engine; runs locally, no network. A frame whose longest side exceeds the engine limit is
/// shrunk to fit and the line bounds are mapped back to the original frame pixels.
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly string? _language;

    // PublicationOnly does not cache a failure: a language pack installed meanwhile is picked up on the next read.
    private readonly Lazy<WinOcr.OcrEngine> _engine;

    /// <param name="language">BCP-47 tag; null reads the user's profile languages.</param>
    public WindowsOcrEngine(string? language)
    {
        _language = language;
        _engine = new Lazy<WinOcr.OcrEngine>(CreateEngine, LazyThreadSafetyMode.PublicationOnly);
    }

    public async Task<OcrResult> RecognizeAsync(Frame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var engine = _engine.Value;

        var scale = FrameScaler.ScaleFor(frame.Width, frame.Height, (int)WinOcr.OcrEngine.MaxImageDimension);
        var width = frame.Width;
        var height = frame.Height;
        byte[] pixels;
        if (scale < 1.0)
        {
            width = Math.Max(1, (int)(frame.Width * scale));
            height = Math.Max(1, (int)(frame.Height * scale));
            pixels = FrameScaler.Downscale(frame.Bgra.Span, frame.Width, frame.Height, frame.Stride, width, height);
        }
        else
        {
            pixels = PackRows(frame);
        }

        // The real ratios, not the scale: the scaled size is truncated to whole pixels.
        var ratioX = frame.Width / (double)width;
        var ratioY = frame.Height / (double)height;

        using var bitmap = WinImaging.SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(), WinImaging.BitmapPixelFormat.Bgra8, width, height);
        var recognised = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken).ConfigureAwait(false);

        var lines = new List<OcrLine>(recognised.Lines.Count);
        foreach (var line in recognised.Lines)
        {
            if (line.Words.Count == 0)
            {
                continue;
            }

            lines.Add(new OcrLine(line.Text, Union(line.Words, ratioX, ratioY, frame.Width, frame.Height)));
        }

        return lines.Count == 0 ? OcrResult.Empty : new OcrResult(lines);
    }

    private WinOcr.OcrEngine CreateEngine()
    {
        if (_language is null)
        {
            return WinOcr.OcrEngine.TryCreateFromUserProfileLanguages()
                ?? throw new OcrLanguageMissingException(FirstProfileLanguage());
        }

        try
        {
            return WinOcr.OcrEngine.TryCreateFromLanguage(new global::Windows.Globalization.Language(_language))
                ?? throw new OcrLanguageMissingException(_language);
        }
        catch (ArgumentException)
        {
            // Not a valid language tag: as unusable as a missing pack.
            throw new OcrLanguageMissingException(_language);
        }
    }

    private static string FirstProfileLanguage() =>
        global::Windows.System.UserProfile.GlobalizationPreferences.Languages is [var first, ..] ? first : string.Empty;

    // CreateCopyFromBuffer reads tightly packed rows.
    private static byte[] PackRows(Frame frame)
    {
        var rowBytes = frame.Width * 4;
        var source = frame.Bgra.Span;
        if (frame.Stride == rowBytes)
        {
            return source[..(rowBytes * frame.Height)].ToArray();
        }

        var packed = new byte[rowBytes * frame.Height];
        for (var y = 0; y < frame.Height; y++)
        {
            source.Slice(y * frame.Stride, rowBytes).CopyTo(packed.AsSpan(y * rowBytes, rowBytes));
        }

        return packed;
    }

    private static PixelRect Union(IReadOnlyList<WinOcr.OcrWord> words, double ratioX, double ratioY, int frameWidth, int frameHeight)
    {
        var left = double.MaxValue;
        var top = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MinValue;
        foreach (var word in words)
        {
            var rect = word.BoundingRect;
            left = Math.Min(left, rect.Left);
            top = Math.Min(top, rect.Top);
            right = Math.Max(right, rect.Right);
            bottom = Math.Max(bottom, rect.Bottom);
        }

        var x0 = Math.Clamp((int)Math.Floor(left * ratioX), 0, frameWidth);
        var y0 = Math.Clamp((int)Math.Floor(top * ratioY), 0, frameHeight);
        var x1 = Math.Clamp((int)Math.Ceiling(right * ratioX), 0, frameWidth);
        var y1 = Math.Clamp((int)Math.Ceiling(bottom * ratioY), 0, frameHeight);
        return new PixelRect(x0, y0, x1 - x0, y1 - y0);
    }
}
