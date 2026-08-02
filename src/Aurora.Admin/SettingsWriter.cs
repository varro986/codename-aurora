using System.Text.Json;
using Aurora.Core.Interfaces;
using Aurora.Core.Settings;

namespace Aurora.Admin;

public sealed class SettingsWriter : ISettingsWriter
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aurora", "settings.json");

    public void Save(SettingsData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, json, System.Text.Encoding.UTF8);
        File.Move(tmp, SettingsPath, overwrite: true);
    }
}
