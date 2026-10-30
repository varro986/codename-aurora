using Aurora.Core.Contracts;
using Aurora.Core.Settings;

namespace Aurora.Core.Tests.Settings;

public sealed class AuroraSettingsParserTests
{
    private static RawSettings Valid() => new()
    {
        Hotkey = "Ctrl+Alt+A",
        ProcessModifier = "Ctrl",
        MonitorModifier = "Shift",
        ReadingIntervalMs = "500",
        OcrLanguage = "",
    };

    [Fact]
    public void Parses_valid_settings()
    {
        var result = AuroraSettingsParser.Parse(Valid());

        Assert.True(result.IsValid);
        Assert.Equal(new HotkeyGesture(KeyModifiers.Control | KeyModifiers.Alt, "A"), result.Settings!.Hotkey);
        Assert.Equal(KeyModifiers.Control, result.Settings.ProcessModifier);
        Assert.Equal(KeyModifiers.Shift, result.Settings.MonitorModifier);
        Assert.Equal(TimeSpan.FromMilliseconds(500), result.Settings.ReadingInterval);
        Assert.Null(result.Settings.OcrLanguage);
    }

    [Theory]
    [InlineData("ctrl + shift + f8", KeyModifiers.Control | KeyModifiers.Shift, "F8")]
    [InlineData("Win+1", KeyModifiers.Windows, "1")]
    public void Parses_hotkey_forms(string value, KeyModifiers modifiers, string key)
    {
        var raw = Valid();
        raw.Hotkey = value;

        var result = AuroraSettingsParser.Parse(raw);

        Assert.Equal(new HotkeyGesture(modifiers, key), result.Settings!.Hotkey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("A")]
    [InlineData("Ctrl+Hyper+A")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Space")]
    public void Rejects_invalid_hotkey(string? value)
    {
        var raw = Valid();
        raw.Hotkey = value;

        var result = AuroraSettingsParser.Parse(raw);

        Assert.Null(result.Settings);
        Assert.Contains(result.Errors, e => e.StartsWith("Aurora:Hotkey", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Win")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_pick_modifier(string? value)
    {
        var raw = Valid();
        raw.ProcessModifier = value;

        var result = AuroraSettingsParser.Parse(raw);

        Assert.Contains(result.Errors, e => e.StartsWith("Aurora:ProcessModifier", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_equal_pick_modifiers()
    {
        var raw = Valid();
        raw.MonitorModifier = "control";

        var result = AuroraSettingsParser.Parse(raw);

        Assert.Single(result.Errors, e => e.Contains("must differ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("soon")]
    [InlineData(null)]
    public void Rejects_invalid_interval(string? value)
    {
        var raw = Valid();
        raw.ReadingIntervalMs = value;

        var result = AuroraSettingsParser.Parse(raw);

        Assert.Contains(result.Errors, e => e.StartsWith("Aurora:ReadingIntervalMs", StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_every_invalid_key_at_once()
    {
        var result = AuroraSettingsParser.Parse(new RawSettings());

        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void Keeps_ocr_language_when_set()
    {
        var raw = Valid();
        raw.OcrLanguage = " it-IT ";

        Assert.Equal("it-IT", AuroraSettingsParser.Parse(raw).Settings!.OcrLanguage);
    }
}
