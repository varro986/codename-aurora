using System.Globalization;
using System.Text.RegularExpressions;
using Aurora.Core.Contracts;

namespace Aurora.Core.Settings;

public sealed record SettingsResult(AuroraSettings? Settings, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Turns <see cref="RawSettings"/> into <see cref="AuroraSettings"/>, with one message per invalid key.</summary>
public static partial class AuroraSettingsParser
{
    private const string Section = RawSettings.SectionName;

    private static readonly Dictionary<string, KeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = KeyModifiers.Control,
        ["Control"] = KeyModifiers.Control,
        ["Alt"] = KeyModifiers.Alt,
        ["Shift"] = KeyModifiers.Shift,
        ["Win"] = KeyModifiers.Windows,
    };

    // Pick modifiers are held while clicking: Windows is excluded because it opens the Start menu.
    private static readonly KeyModifiers[] PickModifiers = [KeyModifiers.Control, KeyModifiers.Shift, KeyModifiers.Alt];

    public static SettingsResult Parse(RawSettings raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var errors = new List<string>();

        var hotkey = ParseHotkey(raw.Hotkey, errors);
        var processModifier = ParsePickModifier(nameof(RawSettings.ProcessModifier), raw.ProcessModifier, errors);
        var monitorModifier = ParsePickModifier(nameof(RawSettings.MonitorModifier), raw.MonitorModifier, errors);
        if (processModifier is { } p && p == monitorModifier)
        {
            errors.Add($"{Section}:{nameof(RawSettings.ProcessModifier)} and {Section}:{nameof(RawSettings.MonitorModifier)} must differ.");
        }

        var interval = ParseInterval(raw.ReadingIntervalMs, errors);
        var language = string.IsNullOrWhiteSpace(raw.OcrLanguage) ? null : raw.OcrLanguage.Trim();
        var logFile = ParseLogFile(raw.LogFile, errors);
        var logMaxBytes = ParseLogMaxBytes(raw.LogMaxKilobytes, errors);

        return errors.Count > 0
            ? new SettingsResult(null, errors)
            : new SettingsResult(
                new AuroraSettings(hotkey!, processModifier!.Value, monitorModifier!.Value, interval!.Value, language, logFile!, logMaxBytes!.Value),
                errors);
    }

    private static HotkeyGesture? ParseHotkey(string? value, List<string> errors)
    {
        const string key = $"{Section}:{nameof(RawSettings.Hotkey)}";
        var parts = (value ?? string.Empty).Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            errors.Add($"{key} '{value}': write one or more modifiers and a key, for example 'Ctrl+Alt+A'.");
            return null;
        }

        var modifiers = KeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            if (!ModifierNames.TryGetValue(part, out var modifier))
            {
                errors.Add($"{key} '{value}': '{part}' is not a modifier; use Ctrl, Alt, Shift or Win.");
                return null;
            }

            modifiers |= modifier;
        }

        var last = parts[^1].ToUpperInvariant();
        if (!KeyPattern().IsMatch(last))
        {
            errors.Add($"{key} '{value}': '{parts[^1]}' is not a key; use a letter, a digit or F1-F24.");
            return null;
        }

        return new HotkeyGesture(modifiers, last);
    }

    private static KeyModifiers? ParsePickModifier(string name, string? value, List<string> errors)
    {
        if (value is not null && ModifierNames.TryGetValue(value.Trim(), out var modifier) && PickModifiers.Contains(modifier))
        {
            return modifier;
        }

        errors.Add($"{Section}:{name} '{value}': use Ctrl, Shift or Alt.");
        return null;
    }

    private static TimeSpan? ParseInterval(string? value, List<string> errors)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ms) && ms > 0)
        {
            return TimeSpan.FromMilliseconds(ms);
        }

        errors.Add($"{Section}:{nameof(RawSettings.ReadingIntervalMs)} '{value}': write a whole number of milliseconds above 0.");
        return null;
    }

    private static string? ParseLogFile(string? value, List<string> errors)
    {
        var path = string.IsNullOrWhiteSpace(value) ? null : Environment.ExpandEnvironmentVariables(value.Trim());
        if (path is not null && Path.IsPathFullyQualified(path))
        {
            return path;
        }

        errors.Add($"{Section}:{nameof(RawSettings.LogFile)} '{value}': write a full path; environment variables such as %LOCALAPPDATA% are allowed.");
        return null;
    }

    private static long? ParseLogMaxBytes(string? value, List<string> errors)
    {
        const int BytesPerKilobyte = 1024;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var kilobytes) && kilobytes > 0)
        {
            return (long)kilobytes * BytesPerKilobyte;
        }

        errors.Add($"{Section}:{nameof(RawSettings.LogMaxKilobytes)} '{value}': write a whole number of kilobytes above 0.");
        return null;
    }

    [GeneratedRegex("^([A-Z0-9]|F([1-9]|1[0-9]|2[0-4]))$")]
    private static partial Regex KeyPattern();
}
