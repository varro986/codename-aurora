using Aurora.Core.Interfaces;
using Aurora.Translation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aurora.Tests.Unit.Translation;

public sealed class ModelManagerTests
{
    private ModelManager Build(string modelCachePath = "") =>
        new(new FakeSettings(modelCachePath), NullLogger<ModelManager>.Instance);

    [Fact]
    public void IsLoaded_Initially_IsFalse()
    {
        var sut = Build();

        Assert.False(sut.IsLoaded);
    }

    [Fact]
    public async Task EnsureLoadedAsync_WithEmptyPath_CompletesWithoutException()
    {
        var sut = Build(modelCachePath: "");

        await sut.EnsureLoadedAsync();

        // stub: IsLoaded is true (Interlocked set it) but no ONNX session loaded
        Assert.True(sut.IsLoaded);
    }

    [Fact]
    public async Task EnsureLoadedAsync_WithNonExistentPath_CompletesWithoutException()
    {
        var sut = Build(modelCachePath: @"C:\does\not\exist\at\all");

        await sut.EnsureLoadedAsync();

        Assert.True(sut.IsLoaded);
    }

    [Fact]
    public async Task EnsureLoadedAsync_CalledTwice_IsIdempotent()
    {
        var sut = Build();

        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();

        Assert.True(sut.IsLoaded);
    }

    [Fact]
    public async Task EnsureLoadedAsync_CalledConcurrently_NeitherThrows()
    {
        var sut = Build();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.EnsureLoadedAsync())
            .ToArray();

        // Must not throw — Interlocked ensures only one call proceeds
        await Task.WhenAll(tasks);
        Assert.True(sut.IsLoaded);
    }

    [Fact]
    public async Task DisposeAsync_ResetsIsLoaded()
    {
        var sut = Build();
        await sut.EnsureLoadedAsync();
        Assert.True(sut.IsLoaded);

        await sut.DisposeAsync();

        Assert.False(sut.IsLoaded);
    }

    private sealed class FakeSettings : IAppSettings
    {
        public FakeSettings(string modelCachePath) => ModelCachePath = modelCachePath;

        public string SourceLanguage => "it";
        public string TargetLanguage => "en";
        public string HotkeyTrigger => "Alt+F1";
        public string HotkeyRullo => "Alt+F2";
        public string PrivateDictionaryPath => "";
        public string GenericDictionaryPath => "";
        public string ModelCachePath { get; }
        public string UpdateChannel => "stable";
        public TimeSpan OverlayDismissTimeout => TimeSpan.FromSeconds(5);
        public string OverlayBackgroundColor => "#CC000000";
        public string OverlayForegroundColor => "#FFFFFFFF";
        public int HoverDwellThreshold => 300;
        public int RulloSamplingInterval => 500;
    }
}
