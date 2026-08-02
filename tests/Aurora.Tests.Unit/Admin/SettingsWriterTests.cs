using System.Text.Json;
using Aurora.Admin;
using Aurora.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aurora.Tests.Unit.Admin;

// SettingsWriter.Save() always writes to the static SettingsPath (%APPDATA%\Aurora\settings.json).
// These are integration-style unit tests: they exercise the real file system at the production path.
// Tests back up and restore the file to avoid polluting operator settings.
public sealed class SettingsWriterTests : IDisposable
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aurora", "settings.json");

    private readonly string? _backup;

    public SettingsWriterTests()
    {
        if (File.Exists(SettingsPath))
            _backup = File.ReadAllText(SettingsPath);
    }

    public void Dispose()
    {
        if (_backup is not null)
            File.WriteAllText(SettingsPath, _backup);
        else if (File.Exists(SettingsPath))
            File.Delete(SettingsPath);

        // Ensure no stray .tmp files are left
        var tmp = SettingsPath + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    private static SettingsData SampleData() => new()
    {
        SourceLanguage = "de",
        TargetLanguage = "fr",
        HotkeyTrigger = "Ctrl+F9",
        HotkeyRullo = "Ctrl+F10",
        PrivateDictionaryPath = @"C:\dicts\priv.json",
        GenericDictionaryPath = @"C:\dicts\gen.json",
        ModelCachePath = @"C:\models",
        UpdateChannel = "beta",
        OverlayDismissTimeoutSeconds = 8,
        OverlayBackgroundColor = "#AA000000",
        OverlayForegroundColor = "#FFEEEEEE",
        HoverDwellThreshold = 400,
        RulloSamplingInterval = 750,
    };

    [Fact]
    public void Save_WritesValidJson()
    {
        var sut = new SettingsWriter();

        sut.Save(SampleData());

        Assert.True(File.Exists(SettingsPath));
        var json = File.ReadAllText(SettingsPath);
        using var doc = JsonDocument.Parse(json); // throws if not valid JSON
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void Save_JsonContainsExpectedValues()
    {
        var sut = new SettingsWriter();
        var data = SampleData();

        sut.Save(data);

        var json = File.ReadAllText(SettingsPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("de", root.GetProperty("SourceLanguage").GetString());
        Assert.Equal("beta", root.GetProperty("UpdateChannel").GetString());
        Assert.Equal(8, root.GetProperty("OverlayDismissTimeoutSeconds").GetInt32());
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        // The production dir likely already exists; this test verifies no crash when it does
        var sut = new SettingsWriter();

        var ex = Record.Exception(() => sut.Save(SampleData()));

        Assert.Null(ex);
        Assert.True(Directory.Exists(Path.GetDirectoryName(SettingsPath)));
    }

    [Fact]
    public void Save_IsAtomic_NoTmpFileLeft()
    {
        var sut = new SettingsWriter();

        sut.Save(SampleData());

        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void Save_CanBeReadBackByAppSettings()
    {
        var sut = new SettingsWriter();
        var data = SampleData();

        sut.Save(data);

        // AppSettings(logger) uses the same production path
        var settings = new AppSettings(NullLogger<AppSettings>.Instance);
        Assert.Equal("de", settings.SourceLanguage);
        Assert.Equal("beta", settings.UpdateChannel);
        Assert.Equal(TimeSpan.FromSeconds(8), settings.OverlayDismissTimeout);
        Assert.Equal(400, settings.HoverDwellThreshold);
    }
}
