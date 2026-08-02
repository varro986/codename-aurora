using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aurora.Translation;

internal static class DictionaryLoader
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public static Dictionary<string, string> Load(string path, ILogger logger)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _opts)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dictionary at '{Path}' is malformed — skipping.", path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
