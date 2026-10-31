using Aurora.Core.Contracts;

namespace Aurora.Core.Settings;

/// <summary>Validated settings; built only by <see cref="AuroraSettingsParser"/>.</summary>
public sealed record AuroraSettings(
    HotkeyGesture Hotkey,
    KeyModifiers ProcessModifier,
    KeyModifiers MonitorModifier,
    TimeSpan ReadingInterval,
    string? OcrLanguage,
    string LogFile,
    long LogMaxBytes);

/// <summary>The "Aurora" section of appsettings.json as written, before validation.</summary>
public sealed class RawSettings
{
    public const string SectionName = "Aurora";

    public string? Hotkey { get; set; }

    public string? ProcessModifier { get; set; }

    public string? MonitorModifier { get; set; }

    public string? ReadingIntervalMs { get; set; }

    public string? OcrLanguage { get; set; }

    public string? LogFile { get; set; }

    public string? LogMaxKilobytes { get; set; }
}
