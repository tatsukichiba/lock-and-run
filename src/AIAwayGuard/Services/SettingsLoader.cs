using System.IO;
using System.Text.Json;
using AIAwayGuard.Models;

namespace AIAwayGuard.Services;

public static class SettingsLoader
{
    private static readonly string[] DefaultProcessNames =
    [
        "codex",
        "code",
        "cursor",
        "ollama",
        "python",
        "node",
        "git",
        "powershell",
        "cmd",
        "wsl",
        "Unity"
    ];

    public static AppSettings Load()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(settingsPath))
        {
            return new AppSettings { MonitoredProcessNames = [.. DefaultProcessNames] };
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var names = settings?.MonitoredProcessNames?
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AppSettings
            {
                MonitoredProcessNames = names is { Count: > 0 } ? names : [.. DefaultProcessNames]
            };
        }
        catch
        {
            return new AppSettings { MonitoredProcessNames = [.. DefaultProcessNames] };
        }
    }
}
