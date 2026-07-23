using System.IO;
using System.Text;
using System.Text.Json;
using LockRun.Models;

namespace LockRun.Services;

public static class SettingsLoader
{
    private const int MinimumSampleIntervalSeconds = 2;
    private const int MaximumSampleIntervalSeconds = 60;
    private const int MinimumReportHistoryLimit = 1;
    private const int MaximumReportHistoryLimit = 100;

    private static readonly string[] DefaultProcessNames =
    [
        "codex",
        "code",
        "cursor",
        "claude",
        "cowork-svc",
        "ollama",
        "python",
        "node",
        "git",
        "powershell",
        "cmd",
        "wsl",
        "Unity"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string UserDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LockRun");

    public static string UserSettingsPath { get; } = Path.Combine(
        UserDataDirectory,
        "appsettings.json");

    public static AppSettings Load(out string? warning)
    {
        var warnings = new List<string>();
        var candidatePaths = new[]
        {
            UserSettingsPath,
            Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        };

        foreach (var settingsPath in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(settingsPath))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
                warning = warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings);
                return Normalize(settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                warnings.Add($"Could not read settings from '{settingsPath}': {ex.Message}");
            }
        }

        warning = warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings);
        return CreateDefault();
    }

    public static void Save(AppSettings settings)
    {
        var normalizedSettings = Normalize(settings);
        Directory.CreateDirectory(UserDataDirectory);

        var temporaryPath = UserSettingsPath + ".tmp";
        var json = JsonSerializer.Serialize(normalizedSettings, SerializerOptions);
        File.WriteAllText(temporaryPath, json + Environment.NewLine, new UTF8Encoding(false));
        File.Move(temporaryPath, UserSettingsPath, true);
    }

    public static AppSettings Normalize(AppSettings? settings)
    {
        var normalizedNames = settings?.MonitoredProcessNames?
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var language = settings?.Language?.Trim().ToLowerInvariant();
        if (language is not ("auto" or "ja" or "en"))
        {
            language = "auto";
        }

        return new AppSettings
        {
            MonitoredProcessNames = normalizedNames is { Count: > 0 }
                ? normalizedNames
                : [.. DefaultProcessNames],
            Language = language,
            SampleIntervalSeconds = Math.Clamp(
                settings?.SampleIntervalSeconds ?? 5,
                MinimumSampleIntervalSeconds,
                MaximumSampleIntervalSeconds),
            ReportHistoryLimit = Math.Clamp(
                settings?.ReportHistoryLimit ?? 30,
                MinimumReportHistoryLimit,
                MaximumReportHistoryLimit)
        };
    }

    public static AppSettings Clone(AppSettings settings)
    {
        var normalized = Normalize(settings);
        return new AppSettings
        {
            MonitoredProcessNames = [.. normalized.MonitoredProcessNames],
            Language = normalized.Language,
            SampleIntervalSeconds = normalized.SampleIntervalSeconds,
            ReportHistoryLimit = normalized.ReportHistoryLimit
        };
    }

    private static AppSettings CreateDefault()
    {
        return Normalize(null);
    }
}
