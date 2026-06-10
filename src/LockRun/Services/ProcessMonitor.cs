using System.Diagnostics;
using LockRun.Models;

namespace LockRun.Services;

public sealed class ProcessMonitor
{
    public ProcessMonitor(IEnumerable<string> processNames)
    {
        ProcessNames = processNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> ProcessNames { get; }

    public IReadOnlyList<MonitoredProcessInfo> GetRunningProcesses()
    {
        var normalizedNames = ProcessNames
            .Select(NormalizeProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matches = new List<MonitoredProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!TryGetProcessName(process, out var processName) ||
                    !normalizedNames.Contains(processName))
                {
                    continue;
                }

                matches.Add(ToProcessInfo(process, processName));
            }
        }

        return matches
            .OrderBy(static process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static process => process.ProcessId)
            .ToList();
    }

    private static string NormalizeProcessName(string name)
    {
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }

    private static bool TryGetProcessName(Process process, out string processName)
    {
        try
        {
            processName = process.ProcessName;
            return true;
        }
        catch
        {
            processName = string.Empty;
            return false;
        }
    }

    private static MonitoredProcessInfo ToProcessInfo(Process process, string processName)
    {
        return new MonitoredProcessInfo(
            processName,
            process.Id,
            TryGetStartTime(process),
            TryGetTotalProcessorTime(process),
            TryGetMainWindowTitle(process));
    }

    private static DateTimeOffset? TryGetStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return null;
        }
    }

    private static string TryGetMainWindowTitle(Process process)
    {
        try
        {
            return process.MainWindowTitle ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static TimeSpan? TryGetTotalProcessorTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
        }
        catch
        {
            return null;
        }
    }
}
