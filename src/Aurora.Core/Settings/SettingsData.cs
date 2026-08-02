namespace Aurora.Core.Settings;

public sealed class SettingsData
{
    public string SourceLanguage { get; set; } = "it";
    public string TargetLanguage { get; set; } = "en";
    public string HotkeyTrigger { get; set; } = "Alt+F1";
    public string HotkeyRullo { get; set; } = "Alt+F2";
    public string PrivateDictionaryPath { get; set; } = "";
    public string GenericDictionaryPath { get; set; } = "";
    public string ModelCachePath { get; set; } = "";
    public string UpdateChannel { get; set; } = "stable";
    public int OverlayDismissTimeoutSeconds { get; set; } = 5;
    public string OverlayBackgroundColor { get; set; } = "#CC000000";
    public string OverlayForegroundColor { get; set; } = "#FFFFFFFF";
    public int HoverDwellThreshold { get; set; } = 300;
    public int RulloSamplingInterval { get; set; } = 500;
}
