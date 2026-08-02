using Aurora.Core;
using Aurora.Core.Interfaces;
using Aurora.Translation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aurora.Tests.Unit.Translation;

public sealed class TranslationEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _privatePath;
    private readonly string _genericPath;

    public TranslationEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _privatePath = Path.Combine(_tempDir, "private.json");
        _genericPath = Path.Combine(_tempDir, "generic.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private TranslationEngine Build(string? privatePath = null, string? genericPath = null)
    {
        var settings = new FakeSettings(privatePath ?? "", genericPath ?? "");
        var modelManager = new ModelManager(settings, NullLogger<ModelManager>.Instance);
        return new TranslationEngine(settings, modelManager, NullLogger<TranslationEngine>.Instance);
    }

    [Fact]
    public async Task Token_InPrivateDict_ReturnsPrivate()
    {
        File.WriteAllText(_privatePath, """{"hello": "ciao"}""");
        using var sut = Build(privatePath: _privatePath);

        var result = await sut.TranslateAsync("hello");

        Assert.Equal("ciao", result.Text);
        Assert.Equal(TranslationSourceLevel.Private, result.SourceLevel);
    }

    [Fact]
    public async Task Token_InGenericOnly_ReturnsGeneric()
    {
        File.WriteAllText(_genericPath, """{"world": "mondo"}""");
        using var sut = Build(genericPath: _genericPath);

        var result = await sut.TranslateAsync("world");

        Assert.Equal("mondo", result.Text);
        Assert.Equal(TranslationSourceLevel.Generic, result.SourceLevel);
    }

    [Fact]
    public async Task Token_NotInAnyDict_NoModel_ReturnsVerbatim()
    {
        using var sut = Build();

        var result = await sut.TranslateAsync("unknown_token");

        Assert.Equal("unknown_token", result.Text);
        Assert.Equal(TranslationSourceLevel.Verbatim, result.SourceLevel);
    }

    [Fact]
    public async Task EmptyText_ReturnsVerbatim()
    {
        using var sut = Build();

        var result = await sut.TranslateAsync("   ");

        Assert.Equal(TranslationSourceLevel.Verbatim, result.SourceLevel);
    }

    [Fact]
    public async Task PrivateTakesPrecedenceOverGeneric()
    {
        File.WriteAllText(_privatePath, """{"word": "private-value"}""");
        File.WriteAllText(_genericPath, """{"word": "generic-value"}""");
        using var sut = Build(privatePath: _privatePath, genericPath: _genericPath);

        var result = await sut.TranslateAsync("word");

        Assert.Equal("private-value", result.Text);
        Assert.Equal(TranslationSourceLevel.Private, result.SourceLevel);
    }

    [Fact(Skip = "Requires [InternalsVisibleTo] or FileSystemWatcher integration — flaky in unit tests. Tracked as tech debt.")]
    public async Task Translate_AfterDictionaryUpdated_ReturnsNewValue()
    {
        // Flow: Build engine with {"key": "old-value"} → overwrite file → trigger reload → expect "new-value".
        // Reload() is private; requires [InternalsVisibleTo] or file-system watcher integration test.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires [InternalsVisibleTo] or FileSystemWatcher integration — flaky in unit tests. Tracked as tech debt.")]
    public async Task Translate_WhenReloadReceivesMalformedFile_RetainsOldDictionary()
    {
        // Flow: valid dict → overwrite with malformed JSON → trigger reload → old value still returned.
        // Reload() is private; requires [InternalsVisibleTo] or file-system watcher integration test.
        await Task.CompletedTask;
    }

    private sealed class FakeSettings : IAppSettings
    {
        public FakeSettings(string privatePath, string genericPath)
        {
            PrivateDictionaryPath = privatePath;
            GenericDictionaryPath = genericPath;
        }

        public string SourceLanguage => "it";
        public string TargetLanguage => "en";
        public string HotkeyTrigger => "Alt+F1";
        public string HotkeyRullo => "Alt+F2";
        public string PrivateDictionaryPath { get; }
        public string GenericDictionaryPath { get; }
        public string ModelCachePath => "";
        public string UpdateChannel => "stable";
        public TimeSpan OverlayDismissTimeout => TimeSpan.FromSeconds(5);
        public string OverlayBackgroundColor => "#CC000000";
        public string OverlayForegroundColor => "#FFFFFFFF";
        public int HoverDwellThreshold => 300;
        public int RulloSamplingInterval => 500;
    }
}
