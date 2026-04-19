using System.IO;
using Newtonsoft.Json;

namespace VPMS.Services;

public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VPMS");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public static string? LoadApiKey()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var json = File.ReadAllText(SettingsPath);
            var obj = JsonConvert.DeserializeObject<PersistedSettings>(json);
            return string.IsNullOrWhiteSpace(obj?.OpenAiApiKey) ? null : obj.OpenAiApiKey;
        }
        catch { return null; }
    }

    public static void SaveApiKey(string apiKey)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var existing = LoadPersistedSettings();
            existing.OpenAiApiKey = apiKey;
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(existing, Formatting.Indented));
        }
        catch { /* non-fatal */ }
    }

    private static PersistedSettings LoadPersistedSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new();
            var json = File.ReadAllText(SettingsPath);
            return JsonConvert.DeserializeObject<PersistedSettings>(json) ?? new();
        }
        catch { return new(); }
    }

    private class PersistedSettings
    {
        public string? OpenAiApiKey { get; set; }
    }
}
