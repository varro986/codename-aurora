using Aurora.Core.Settings;
using Microsoft.Extensions.Configuration;

namespace Aurora.App;

internal static class SettingsLoader
{
    public static SettingsResult Load(string baseDirectory)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            // A value that differs per machine (or per test run) overrides the file: Aurora__LogFile, Aurora__Hotkey...
            .AddEnvironmentVariables()
            .Build();
        var raw = configuration.GetSection(RawSettings.SectionName).Get<RawSettings>() ?? new RawSettings();
        return AuroraSettingsParser.Parse(raw);
    }
}
