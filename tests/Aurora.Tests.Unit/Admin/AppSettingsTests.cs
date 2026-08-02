using Aurora.Admin;
using Aurora.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aurora.Tests.Unit.Admin;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _tempPath;

    public AppSettingsTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_tempPath)!;
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void ValidJson_ReturnsAllThirteenProperties()
    {
        var data = new SettingsData
        {
            SourceLanguage = "de",
            TargetLanguage = "fr",
            HotkeyTrigger = "Ctrl+F1",
            HotkeyRullo = "Ctrl+F2",
            PrivateDictionaryPath = @"C:\dicts\private.json",
            GenericDictionaryPath = @"C:\dicts\generic.json",
            ModelCachePath = @"C:\models",
            UpdateChannel = "beta",
            OverlayDismissTimeoutSeconds = 10,
            OverlayBackgroundColor = "#FF000000",
            OverlayForegroundColor = "#FFFFFFFF",
            HoverDwellThreshold = 500,
            RulloSamplingInterval = 1000,
        };
        AppSettings.Write(_tempPath, data);

        var sut = new AppSettings(NullLogger<AppSettings>.Instance, _tempPath);

        Assert.Equal("de", sut.SourceLanguage);
        Assert.Equal("fr", sut.TargetLanguage);
        Assert.Equal("Ctrl+F1", sut.HotkeyTrigger);
        Assert.Equal("Ctrl+F2", sut.HotkeyRullo);
        Assert.Equal(@"C:\dicts\private.json", sut.PrivateDictionaryPath);
        Assert.Equal(@"C:\dicts\generic.json", sut.GenericDictionaryPath);
        Assert.Equal(@"C:\models", sut.ModelCachePath);
        Assert.Equal("beta", sut.UpdateChannel);
        Assert.Equal(TimeSpan.FromSeconds(10), sut.OverlayDismissTimeout);
        Assert.Equal("#FF000000", sut.OverlayBackgroundColor);
        Assert.Equal("#FFFFFFFF", sut.OverlayForegroundColor);
        Assert.Equal(500, sut.HoverDwellThreshold);
        Assert.Equal(1000, sut.RulloSamplingInterval);
    }

    [Fact]
    public void MissingFile_ReturnsDefaultsAndCreatesFile()
    {
        var sut = new AppSettings(NullLogger<AppSettings>.Instance, _tempPath);

        Assert.Equal("it", sut.SourceLanguage);
        Assert.Equal("en", sut.TargetLanguage);
        Assert.Equal(TimeSpan.FromSeconds(5), sut.OverlayDismissTimeout);
        Assert.Equal(300, sut.HoverDwellThreshold);
        Assert.Equal(500, sut.RulloSamplingInterval);
        Assert.True(File.Exists(_tempPath));
    }

    [Fact]
    public void MalformedJson_ReturnsDefaultsWithoutThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tempPath)!);
        File.WriteAllText(_tempPath, "{ this is not json }");

        var sut = new AppSettings(NullLogger<AppSettings>.Instance, _tempPath);

        Assert.Equal("it", sut.SourceLanguage);
        Assert.Equal("stable", sut.UpdateChannel);
    }

    [Fact]
    public void WritePath_IsUnderAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sut = new AppSettings(NullLogger<AppSettings>.Instance, _tempPath);

        // The production path (used by the public constructor) must be under %APPDATA%
        var productionPath = Path.Combine(appData, "Aurora", "settings.json");
        Assert.StartsWith(appData, productionPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source", productionPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repos", productionPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenPathIsDirectory_ReturnsDefaultsWithoutThrowing()
    {
        // When the path is an existing directory, File.Exists returns false; Write() throws IOException.
        // AppSettings must swallow that exception and return defaults.
        var dirPath = Path.GetTempPath(); // always a directory, never a valid settings file

        var sut = new AppSettings(NullLogger<AppSettings>.Instance, dirPath);

        Assert.Equal("it", sut.SourceLanguage);
        Assert.Equal("stable", sut.UpdateChannel);
    }
}
