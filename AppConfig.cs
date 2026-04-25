using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace WinScrobb;

public class AppConfig
{
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string SessionKey { get; set; } = "";
    public string Username { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 3;
    public bool RunAtStartup { get; set; } = true;

    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupName = "WinScrobb";

    public void ApplyStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupKey, writable: true);
        if (key is null) return;

        if (RunAtStartup)
        {
            var exe = Environment.ProcessPath ?? "";
            if (!string.IsNullOrEmpty(exe))
                key.SetValue(StartupName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(StartupName, throwOnMissingValue: false);
        }
    }

    // -------------------------------------------------------------------------

    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinScrobb");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiSecret);

    [JsonIgnore]
    public bool IsAuthenticated => !string.IsNullOrEmpty(SessionKey);
}
