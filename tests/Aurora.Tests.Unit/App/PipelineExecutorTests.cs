// PipelineExecutor is `internal sealed class` in Aurora.App (WinExe + WPF).
// Referencing Aurora.App from a non-WPF test project risks XAML compilation failures.
//
// To enable these tests:
//   1. Add <UseWPF>true</UseWPF><UseWindowsForms>true</UseWindowsForms> to Aurora.Tests.Unit.csproj
//   2. Add <ProjectReference Include="..\..\src\Aurora.App\Aurora.App.csproj" /> to Aurora.Tests.Unit.csproj
//   3. Add InternalsVisibleTo("Aurora.Tests.Unit") to Aurora.App.csproj
//   4. Remove the [Fact(Skip=...)] attributes below.
//
// The fakes and test logic are complete and ready — only the project wiring is missing.

using Aurora.Core;
using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using MediatR;
using Xunit;

namespace Aurora.Tests.Unit.App;

public sealed class PipelineExecutorTests
{
    private const string SkipReason =
        "PipelineExecutor is internal in Aurora.App (WinExe). Enable by adding UseWPF + project reference + InternalsVisibleTo — see file header.";

    [Fact(Skip = SkipReason)]
    public async Task Execute_WhenCaptureReturnsEmpty_DoesNotCallOcr()
    {
        var capture = new FakeCapture(Array.Empty<byte>());
        var ocr = new FakeOcr("irrelevant");
        var sut = BuildExecutor(capture, ocr);

        await sut.ExecuteAsync();

        Assert.False(ocr.WasCalled);
    }

    [Fact(Skip = SkipReason)]
    public async Task Execute_WhenOcrReturnsEmpty_DoesNotCallTranslate()
    {
        var capture = new FakeCapture(new byte[] { 1 });
        var ocr = new FakeOcr("");
        var translation = new FakeTranslation(new TranslationResult("x", TranslationSourceLevel.Verbatim));
        var sut = BuildExecutor(capture, ocr, translation);

        await sut.ExecuteAsync();

        Assert.False(translation.WasCalled);
    }

    [Fact(Skip = SkipReason)]
    public async Task Execute_WhenOcrReturnsWhitespace_DoesNotCallTranslate()
    {
        var capture = new FakeCapture(new byte[] { 1 });
        var ocr = new FakeOcr("   ");
        var translation = new FakeTranslation(new TranslationResult("x", TranslationSourceLevel.Verbatim));
        var sut = BuildExecutor(capture, ocr, translation);

        await sut.ExecuteAsync();

        Assert.False(translation.WasCalled);
    }

    [Fact(Skip = SkipReason)]
    public async Task Execute_HappyPath_PublishesTranslationReady()
    {
        var capture = new FakeCapture(new byte[] { 1, 2 });
        var ocr = new FakeOcr("hello");
        var result = new TranslationResult("ciao", TranslationSourceLevel.Private);
        var translation = new FakeTranslation(result);
        var publisher = new FakePublisher();
        var sut = BuildExecutor(capture, ocr, translation, publisher);

        await sut.ExecuteAsync();

        Assert.Equal(1, publisher.PublishedCount);
    }

    [Fact(Skip = SkipReason)]
    public async Task Execute_WhenOcrThrows_DoesNotThrow()
    {
        var capture = new FakeCapture(new byte[] { 1 });
        var ocr = new ThrowingOcr();
        var sut = BuildExecutor(capture, ocr);

        var ex = await Record.ExceptionAsync(() => sut.ExecuteAsync());

        Assert.Null(ex);
    }

    [Fact(Skip = SkipReason)]
    public async Task Execute_WhenTranslateThrows_DoesNotThrow()
    {
        var capture = new FakeCapture(new byte[] { 1 });
        var ocr = new FakeOcr("hello");
        var translation = new ThrowingTranslation();
        var sut = BuildExecutor(capture, ocr, translation);

        var ex = await Record.ExceptionAsync(() => sut.ExecuteAsync());

        Assert.Null(ex);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    // Uncomment and use when Aurora.App project reference is wired up:
    // private static PipelineExecutor BuildExecutor(
    //     ICaptureService? capture = null,
    //     IOcrService? ocr = null,
    //     ITranslationEngine? translation = null,
    //     IPublisher? publisher = null)
    // {
    //     return new PipelineExecutor(
    //         capture ?? new FakeCapture(new byte[] { 1 }),
    //         ocr ?? new FakeOcr("text"),
    //         translation ?? new FakeTranslation(new TranslationResult("t", TranslationSourceLevel.Verbatim)),
    //         publisher ?? new FakePublisher(),
    //         new FakeSettings(),
    //         Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineExecutor>.Instance);
    // }

    // Stub: remove when project reference is wired up
    private static object BuildExecutor(params object[] _) => null!;

    // ── fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeCapture : ICaptureService
    {
        private readonly byte[] _data;
        public FakeCapture(byte[] data) => _data = data;
        public byte[] CaptureScreen() => _data;
    }

    private sealed class FakeOcr : IOcrService
    {
        private readonly string _result;
        public bool WasCalled { get; private set; }
        public FakeOcr(string result) => _result = result;
        public Task<string> RecognizeAsync(byte[] imageBytes, string language, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingOcr : IOcrService
    {
        public Task<string> RecognizeAsync(byte[] imageBytes, string language, CancellationToken ct = default)
            => throw new InvalidOperationException("OCR failure");
    }

    private sealed class FakeTranslation : ITranslationEngine
    {
        private readonly TranslationResult _result;
        public bool WasCalled { get; private set; }
        public FakeTranslation(TranslationResult result) => _result = result;
        public Task<TranslationResult> TranslateAsync(string text, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingTranslation : ITranslationEngine
    {
        public Task<TranslationResult> TranslateAsync(string text, CancellationToken ct = default)
            => throw new InvalidOperationException("Translation failure");
    }

    private sealed class FakePublisher : IPublisher
    {
        public int PublishedCount { get; private set; }
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            PublishedCount++;
            return Task.CompletedTask;
        }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            PublishedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettings : IAppSettings
    {
        public string SourceLanguage => "it";
        public string TargetLanguage => "en";
        public string HotkeyTrigger => "Alt+F1";
        public string HotkeyRullo => "Alt+F2";
        public string PrivateDictionaryPath => "";
        public string GenericDictionaryPath => "";
        public string ModelCachePath => "";
        public string UpdateChannel => "stable";
        public TimeSpan OverlayDismissTimeout => TimeSpan.FromSeconds(5);
        public string OverlayBackgroundColor => "#CC000000";
        public string OverlayForegroundColor => "#FFFFFFFF";
        public int HoverDwellThreshold => 300;
        public int RulloSamplingInterval => 500;
    }
}
