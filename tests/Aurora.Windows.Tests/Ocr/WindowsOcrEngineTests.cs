using System.Runtime.InteropServices.WindowsRuntime;
using Aurora.Core.Contracts;
using Aurora.Windows.Ocr;
using WinImaging = global::Windows.Graphics.Imaging;
using WinOcr = global::Windows.Media.Ocr;
using WinStorage = global::Windows.Storage;

namespace Aurora.Windows.Tests.Ocr;

public sealed class WindowsOcrEngineTests
{
    private const string English = "en-US";
    private const double CentreTolerance = 6;

    private static readonly string[] NeverInstalledCandidates = ["ja-JP", "ko-KR", "zh-CN", "ar-SA", "hi-IN", "th-TH"];

    // Rectangles drawn by Fixtures/generate-fixtures.ps1 (x, y, width, height).
    private static readonly PixelRect HelloRect = new(40, 40, 145, 32);
    private static readonly PixelRect SecondRect = new(40, 120, 231, 32);

    [Fact]
    public async Task Recognize_PrintedText_ReturnsEveryLineWithItsPosition()
    {
        RequireEnglishInstalled();
        var frame = await LoadFrameAsync("two-lines.png");

        var result = await new WindowsOcrEngine(English).RecognizeAsync(frame, TestContext.Current.CancellationToken);

        Assert.Equal(["Hello World", "Second line of text"], result.Lines.Select(l => l.Text));
        AssertAround(HelloRect, result.Lines[0].Bounds);
        AssertAround(SecondRect, result.Lines[1].Bounds);
    }

    [Fact]
    public async Task Recognize_ImageWithoutText_ReturnsNoLines()
    {
        RequireEnglishInstalled();
        var frame = await LoadFrameAsync("blank.png");

        var result = await new WindowsOcrEngine(English).RecognizeAsync(frame, TestContext.Current.CancellationToken);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task Recognize_RowsWithPadding_ReadsTheSameLines()
    {
        RequireEnglishInstalled();
        var tight = await LoadFrameAsync("two-lines.png");
        const int padding = 64;
        var stride = tight.Stride + padding;
        var padded = new byte[stride * tight.Height];
        for (var y = 0; y < tight.Height; y++)
        {
            tight.Bgra.Span.Slice(y * tight.Stride, tight.Stride).CopyTo(padded.AsSpan(y * stride));
        }

        var result = await new WindowsOcrEngine(English).RecognizeAsync(
            new Frame(tight.Width, tight.Height, stride, padded), TestContext.Current.CancellationToken);

        Assert.Equal(["Hello World", "Second line of text"], result.Lines.Select(l => l.Text));
    }

    [Fact]
    public async Task Recognize_FrameLargerThanEngineLimit_MapsBoundsBackToFramePixels()
    {
        RequireEnglishInstalled();
        var source = await LoadFrameAsync("two-lines.png");
        var width = (int)WinOcr.OcrEngine.MaxImageDimension + 300;
        const int height = 2000;
        var stride = width * 4;
        var wide = new byte[stride * height];
        Array.Fill(wide, (byte)255);
        for (var y = 0; y < source.Height; y++)
        {
            source.Bgra.Span.Slice(y * source.Stride, source.Stride).CopyTo(wide.AsSpan(y * stride));
        }

        var result = await new WindowsOcrEngine(English).RecognizeAsync(
            new Frame(width, height, stride, wide), TestContext.Current.CancellationToken);

        Assert.Equal(["Hello World", "Second line of text"], result.Lines.Select(l => l.Text));
        AssertAround(HelloRect, result.Lines[0].Bounds);
        AssertAround(SecondRect, result.Lines[1].Bounds);
    }

    [Fact]
    public async Task Recognize_LanguageNotInstalled_ThrowsOcrLanguageMissing()
    {
        var installed = WinOcr.OcrEngine.AvailableRecognizerLanguages
            .Select(l => l.LanguageTag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = NeverInstalledCandidates.First(t => !installed.Contains(t));

        var error = await Assert.ThrowsAsync<OcrLanguageMissingException>(
            () => new WindowsOcrEngine(missing).RecognizeAsync(BlankFrame(), TestContext.Current.CancellationToken));

        Assert.Equal(missing, error.Language);
    }

    [Fact]
    public async Task Recognize_InvalidLanguageTag_ThrowsOcrLanguageMissing()
    {
        var error = await Assert.ThrowsAsync<OcrLanguageMissingException>(
            () => new WindowsOcrEngine("not a tag").RecognizeAsync(BlankFrame(), TestContext.Current.CancellationToken));

        Assert.Equal("not a tag", error.Language);
    }

    private static Frame BlankFrame() => new(8, 8, 32, new byte[8 * 32]);

    // A machine set up for the desktop step has it: a missing pack is a failure, not a skip.
    private static void RequireEnglishInstalled() =>
        Assert.True(
            WinOcr.OcrEngine.AvailableRecognizerLanguages.Any(l => string.Equals(l.LanguageTag, English, StringComparison.OrdinalIgnoreCase)),
            $"OCR language {English} is not installed: add it in Settings > Time & language > Language & region.");

    // Centres and heights, not edges: the drawn rectangles are GDI text boxes, the OCR boxes hug the glyphs.
    private static void AssertAround(PixelRect expected, PixelRect actual)
    {
        Assert.InRange(actual.X + (actual.Width / 2.0), expected.X + (expected.Width / 2.0) - CentreTolerance, expected.X + (expected.Width / 2.0) + CentreTolerance);
        Assert.InRange(actual.Y + (actual.Height / 2.0), expected.Y + (expected.Height / 2.0) - CentreTolerance, expected.Y + (expected.Height / 2.0) + CentreTolerance);
        Assert.InRange(actual.Height, expected.Height * 0.5, expected.Height * 1.5);
    }

    private static async Task<Frame> LoadFrameAsync(string fixture)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Ocr", "Fixtures", fixture);
        var file = await WinStorage.StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await WinImaging.BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            WinImaging.BitmapPixelFormat.Bgra8, WinImaging.BitmapAlphaMode.Premultiplied);
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyToBuffer(bytes.AsBuffer());
        return new Frame(bitmap.PixelWidth, bitmap.PixelHeight, bitmap.PixelWidth * 4, bytes);
    }
}
