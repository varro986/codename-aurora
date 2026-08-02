using System.Text.Json;
using Aurora.Core.Interfaces;
using Aurora.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Aurora.Admin;

public sealed class AppSettings : IAppSettings
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aurora", "settings.json");

    private readonly SettingsData _data;
    private readonly string _path;

    public AppSettings(ILogger<AppSettings> logger) : this(logger, DefaultPath) { }

    internal AppSettings(ILogger<AppSettings> logger, string path)
    {
        _path = path;
        _data = Load(logger, path);
    }

    public string SourceLanguage => _data.SourceLanguage;
    public string TargetLanguage => _data.TargetLanguage;
    public string HotkeyTrigger => _data.HotkeyTrigger;
    public string HotkeyRullo => _data.HotkeyRullo;
    public string PrivateDictionaryPath => _data.PrivateDictionaryPath;
    public string GenericDictionaryPath => _data.GenericDictionaryPath;
    public string ModelCachePath => _data.ModelCachePath;
    public string UpdateChannel => _data.UpdateChannel;
    public TimeSpan OverlayDismissTimeout => TimeSpan.FromSeconds(_data.OverlayDismissTimeoutSeconds);
    public string OverlayBackgroundColor => _data.OverlayBackgroundColor;
    public string OverlayForegroundColor => _data.OverlayForegroundColor;
    public int HoverDwellThreshold => _data.HoverDwellThreshold;
    public int RulloSamplingInterval => _data.RulloSamplingInterval;

    private static SettingsData Load(ILogger logger, string path)
    {
        if (!File.Exists(path))
        {
            var defaults = new SettingsData();
            try { Write(path, defaults); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not persist default settings to '{Path}'.", path); }
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load settings from '{Path}' — using defaults.", path);
            return new SettingsData();
        }
    }

    internal static void Write(string path, SettingsData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json, System.Text.Encoding.UTF8);
        File.Move(tmp, path, overwrite: true);
    }
}
