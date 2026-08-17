using System.IO;
using System.Text;

namespace LockRun.Services;

public static class ReportStore
{
    public static string ReportsDirectory { get; } = Path.Combine(
        SettingsLoader.UserDataDirectory,
        "Reports");

    public static string SaveSummary(
        DateTimeOffset startedAt,
        string summary,
        int historyLimit,
        out string? cleanupWarning)
    {
        Directory.CreateDirectory(ReportsDirectory);

        var fileName = $"away-{startedAt:yyyyMMdd-HHmmss-fff}.txt";
        var reportPath = Path.Combine(ReportsDirectory, fileName);
        File.WriteAllText(reportPath, summary + Environment.NewLine, new UTF8Encoding(false));

        cleanupWarning = TryTrimHistory(historyLimit);
        return reportPath;
    }

    public static bool TryLoadLatest(
        out string summary,
        out string? reportPath,
        out string? warning)
    {
        summary = string.Empty;
        reportPath = null;
        warning = null;

        if (!Directory.Exists(ReportsDirectory))
        {
            return false;
        }

        try
        {
            reportPath = Directory
                .EnumerateFiles(ReportsDirectory, "away-*.txt")
                .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (reportPath is null)
            {
                return false;
            }

            summary = File.ReadAllText(reportPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warning = ex.Message;
            reportPath = null;
            return false;
        }
    }

    public static int DeleteHistory()
    {
        return DeleteHistory(ReportsDirectory);
    }

    internal static int DeleteHistory(string reportsDirectory)
    {
        if (!Directory.Exists(reportsDirectory))
        {
            return 0;
        }

        var reportPaths = Directory
            .EnumerateFiles(reportsDirectory, "away-*.txt")
            .ToList();

        foreach (var reportPath in reportPaths)
        {
            File.Delete(reportPath);
        }

        return reportPaths.Count;
    }

    private static string? TryTrimHistory(int historyLimit)
    {
        try
        {
            var obsoleteReports = Directory
                .EnumerateFiles(ReportsDirectory, "away-*.txt")
                .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                .Skip(Math.Max(1, historyLimit));

            foreach (var obsoleteReport in obsoleteReports)
            {
                File.Delete(obsoleteReport);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }
}
