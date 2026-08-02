namespace Aurora.Core.Interfaces;

public interface IAppSettings
{
    string SourceLanguage { get; }
    string TargetLanguage { get; }
    string HotkeyTrigger { get; }
    string HotkeyRullo { get; }
    string PrivateDictionaryPath { get; }
    string GenericDictionaryPath { get; }
    string ModelCachePath { get; }
    string UpdateChannel { get; }
    TimeSpan OverlayDismissTimeout { get; }
    string OverlayBackgroundColor { get; }
    string OverlayForegroundColor { get; }
    int HoverDwellThreshold { get; }
    int RulloSamplingInterval { get; }
}
